using Dapper;
using Ecare.Application.Services;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.Flux;

public sealed class UpdateSecondWeightByBonHandler
    : IRequestHandler<UpdateSecondWeightByBonCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UpdateSecondWeightByBonHandler> _log;
    private readonly ServiceManager _serviceManager;

    public UpdateSecondWeightByBonHandler(
        IUnitOfWork uow,
        ILogger<UpdateSecondWeightByBonHandler> log,
        ServiceManager serviceManager)
    {
        _uow = uow;
        _log = log;
        _serviceManager = serviceManager;
    }

    public async Task<Result<int>> Handle(UpdateSecondWeightByBonCommand request, CancellationToken ct)
    {
        await _uow.BeginAsync(ct);

        try
        {
            // 1) Update EcareFlux and capture related OrderIds + Ligne
            const string sqlFlux = @"
                ;WITH x AS (
                    SELECT TOP (1) *
                    FROM dbo.EcareFlux
                    WHERE Matricule = @Matricule
                      AND BonDeCommande = @BonDeCommande
                    ORDER BY ParkedAt DESC
                )
                UPDATE x
                SET 
                    SecondWeight = @SecondWeight,
                    TotalCharged = @SecondWeight - @FirstWeight,
                    PabExitAt = @Now
                OUTPUT INSERTED.OrderId, INSERTED.Ligne;";

            var fluxResults = (await _uow.Connection.QueryAsync<FluxUpdateRow>(
                    sqlFlux,
                    new
                    {
                        request.Matricule,
                        request.BonDeCommande,
                        request.SecondWeight,
                        request.FirstWeight,
                        Now = DateTime.Now
                    },
                    _uow.Transaction))
                .ToList();

            var orderIds = fluxResults
                .Where(r => r.OrderId.HasValue)
                .Select(r => r.OrderId!.Value)
                .Distinct()
                .ToArray();

            if (orderIds.Length == 0)
            {
                await _uow.RollbackAsync(ct);
                return Result<int>.Fail("No EcareFlux row matched Matricule + BonDeCommande or no OrderId found.");
            }

            // 2) Mark related orders as 'Termine'
            const string sqlOrders = @"
                UPDATE dbo.Orders
                SET Statut = @Termine
                WHERE Id IN @OrderIds;";

            var affectedOrders = await _uow.Connection.ExecuteAsync(
                sqlOrders,
                new
                {
                    OrderIds = orderIds,
                    Termine = "Termine"
                },
                _uow.Transaction);

            // 3) Release one realtime slot of the corresponding ligne(s).
            // Capacity is the nominal maximum and must not be mutated by truck flow.
            var ligneNames = fluxResults
                .Select(r => r.Ligne)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct()
                .ToArray();

            if (ligneNames.Length > 0)
            {
                const string sqlCap = @"
                    UPDATE dbo.Ecare_Ligne
                    SET RealtimeCapacity =
                        CASE
                            WHEN ISNULL(RealtimeCapacity, 0) < ISNULL(Capacity, 0)
                                THEN ISNULL(RealtimeCapacity, 0) + 1
                            ELSE ISNULL(Capacity, 0)
                        END
                    WHERE Nom IN @LigneNames;";

                await _uow.Connection.ExecuteAsync(
                    sqlCap,
                    new { LigneNames = ligneNames },
                    _uow.Transaction);
            }

            await _uow.CommitAsync(ct);

            // You can return affectedOrders or orderIds.Length — they should be aligned.
            return Result<int>.Ok(affectedOrders);
        }
        catch (Exception ex)
        {
            try { await _uow.RollbackAsync(ct); } catch { }

            _log.LogError(
                ex,
                "Failed to update SecondWeight, close orders and update capacity for {Matricule}/{Bon}",
                request.Matricule,
                request.BonDeCommande);

            return Result<int>.Fail("Database error while updating SecondWeight, order statuses and line capacity.");
        }
    }

    private sealed class FluxUpdateRow
    {
        public int? OrderId { get; init; }
        public string? Ligne { get; init; }
    }
}
