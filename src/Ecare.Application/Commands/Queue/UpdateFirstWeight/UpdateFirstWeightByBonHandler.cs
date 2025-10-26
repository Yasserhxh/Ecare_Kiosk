// Ecare.Application/Commands/Flux/UpdateFirstWeightByBonHandler.cs
using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Commands.Flux;

public sealed class UpdateFirstWeightByBonHandler
    : IRequestHandler<UpdateFirstWeightByBonCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UpdateFirstWeightByBonHandler> _log;

    public UpdateFirstWeightByBonHandler(IUnitOfWork uow, ILogger<UpdateFirstWeightByBonHandler> log)
    {
        _uow = uow;
        _log = log;
    }

    public async Task<Result<int>> Handle(UpdateFirstWeightByBonCommand request, CancellationToken ct)
    {
        await _uow.BeginAsync(ct);
        try
        {
            // 1) Update EcareFlux by Matricule + BonDeCommande
            const string sqlFlux = @"
UPDATE dbo.EcareFlux
SET FirstWeight = @FirstWeight,
    PabEntryAt = @Now
WHERE Matricule = @Matricule
  AND BonDeCommande = @BonDeCommande;";

            var fluxRows = await _uow.Connection.ExecuteAsync(
                sqlFlux,
                new
                {
                    request.Matricule,
                    request.BonDeCommande,
                    request.FirstWeight,
                    Now = DateTime.Now
                },
                _uow.Transaction);

            if (fluxRows == 0)
            {
                await _uow.RollbackAsync(ct);
                return Result<int>.Fail("No EcareFlux row matched Matricule + BonDeCommande.");
            }

            // 2) Update Ecare_Queue status=2 by Matricule + Bon_Commande (= same order number)
            const string sqlQueue = @"
UPDATE dbo.Ecare_Queue
SET Status = 2
WHERE Matricule = @Matricule
  AND Bon_Commande = @BonDeCommande;";

            var queueRows = await _uow.Connection.ExecuteAsync(
                sqlQueue,
                new
                {
                    request.Matricule,
                    request.BonDeCommande
                },
                _uow.Transaction);

            await _uow.CommitAsync(ct);

            _log.LogInformation(
                "FirstWeight updated for {Matricule}/{Bon}. Flux={FluxRows}, Queue updated={QueueRows}",
                request.Matricule, request.BonDeCommande, fluxRows, queueRows);

            return Result<int>.Ok(fluxRows + queueRows);
        }
        catch (Exception ex)
        {
            try { await _uow.RollbackAsync(ct); } catch { }
            _log.LogError(ex, "Failed to update FirstWeight for {Matricule}/{Bon}", request.Matricule, request.BonDeCommande);
            return Result<int>.Fail("Database error while updating FirstWeight and queue status.");
        }
    }
}
