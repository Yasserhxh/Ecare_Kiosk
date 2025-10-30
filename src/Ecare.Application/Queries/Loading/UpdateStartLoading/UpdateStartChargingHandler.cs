using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Queries.Loading.UpdateStartLoading;

public sealed class UpdateStartChargingHandler
    : IRequestHandler<UpdateStartChargingCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UpdateStartChargingHandler> _log;

    public UpdateStartChargingHandler(IUnitOfWork uow, ILogger<UpdateStartChargingHandler> log)
    {
        _uow = uow;
        _log = log;
    }

    public async Task<Result<int>> Handle(UpdateStartChargingCommand request, CancellationToken ct)
    {
        await _uow.BeginAsync(ct);
        try
        {
            const string sql = @"
                UPDATE dbo.EcareFlux
                SET StartChargingAt = @Now
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
