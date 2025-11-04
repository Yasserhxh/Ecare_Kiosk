using Dapper;
using Ecare.Application.Services;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Commands.Flux;

public sealed class UpdateSecondWeightByBonHandler
    : IRequestHandler<UpdateSecondWeightByBonCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UpdateFirstWeightByBonHandler> _log;
    private readonly ServiceManager _serviceManager;

    public UpdateSecondWeightByBonHandler(IUnitOfWork uow, ILogger<UpdateFirstWeightByBonHandler> log, ServiceManager serviceManager)
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
            const string sqlFlux = @"
            UPDATE dbo.EcareFlux
            SET 
                SecondWeight = @SecondWeight,
                TotalCharged = @SecondWeight - @FirstWeight,
                PabExitAt = @Now
            WHERE 
                Matricule = @Matricule
                AND BonDeCommande = @BonDeCommande;";

            var fluxRows = await _uow.Connection.ExecuteAsync(
                sqlFlux,
                new
                {
                    request.Matricule,
                    request.BonDeCommande,
                    request.SecondWeight,
                    request.FirstWeight,
                    Now = DateTime.Now
                },
                _uow.Transaction);

            if (fluxRows == 0)
            {
                await _uow.RollbackAsync(ct);
                return Result<int>.Fail("No EcareFlux row matched Matricule + BonDeCommande.");
            }

            await _uow.CommitAsync(ct);
            return Result<int>.Ok(fluxRows);
        }
        catch (Exception ex)
        {
            try { await _uow.RollbackAsync(ct); } catch { }
            _log.LogError(ex, "Failed to update SecondWeight for {Matricule}/{Bon}", request.Matricule, request.BonDeCommande);
            return Result<int>.Fail("Database error while updating SecondWeight and queue status.");
        }
    }

}
