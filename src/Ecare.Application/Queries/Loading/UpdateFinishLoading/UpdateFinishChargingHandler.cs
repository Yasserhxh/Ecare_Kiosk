using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Commands.Flux;

public sealed class UpdateFinishChargingHandler
    : IRequestHandler<UpdateFinishChargingCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UpdateFinishChargingHandler> _log;

    public UpdateFinishChargingHandler(IUnitOfWork uow, ILogger<UpdateFinishChargingHandler> log)
    {
        _uow = uow;
        _log = log;
    }

    public async Task<Result<int>> Handle(UpdateFinishChargingCommand request, CancellationToken ct)
    {
        await _uow.BeginAsync(ct);
        try
        {
            const string sql = @"
                UPDATE dbo.EcareFlux
                SET FinishedChargingAt = @Now
                WHERE Matricule = @Matricule
                  AND BonDeCommande = @BonDeCommande;";

            var affected = await _uow.Connection.ExecuteAsync(
                sql,
                new
                {
                    request.Matricule,
                    request.BonDeCommande,
                    Now = DateTime.Now
                },
                _uow.Transaction
            );

            if (affected == 0)
            {
                await _uow.RollbackAsync(ct);
                return Result<int>.Fail("Aucune ligne correspondante trouvée dans EcareFlux (Matricule + BonDeCommande).");
            }

            await _uow.CommitAsync(ct);
            _log.LogInformation("StartChargingAt updated for {Matricule}/{BonDeCommande}", request.Matricule, request.BonDeCommande);

            return Result<int>.Ok(affected);
        }
        catch (Exception ex)
        {
            try { await _uow.RollbackAsync(ct); } catch { }
            _log.LogError(ex, "Error updating StartChargingAt for {Matricule}/{BonDeCommande}", request.Matricule, request.BonDeCommande);
            return Result<int>.Fail("Erreur base de données lors de la mise à jour du StartChargingAt.");
        }
    }
}
