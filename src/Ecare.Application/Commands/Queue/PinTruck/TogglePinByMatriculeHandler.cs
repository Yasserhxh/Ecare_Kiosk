using Dapper;
using Ecare.Application.Commands.Queue.PinTruck;
using Ecare.Application.Services;                
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Commands.Queue.TogglePin;

public sealed class TogglePinByMatriculeHandler(
    IUnitOfWork uow,
    ServiceManager signalR,
    ILogger<TogglePinByMatriculeHandler> log)
    : IRequestHandler<TogglePinByMatriculeCommand, Result<int>>
{
    private const string ToggleSql = @"
        DECLARE @Now DATETIME2 = SYSUTCDATETIME();
        DECLARE @ShouldPin BIT = 1;

        SELECT TOP (1)
            @ShouldPin = CASE WHEN ISNULL(IsPined, 0) = 1 THEN 0 ELSE 1 END
        FROM dbo.Ecare_Order_Legend
        WHERE Matricule = @Matricule
          AND ISNULL(Step, 0) = 1
          AND ISNULL(BonDeLivraison, '') = ''
          AND ISNULL(AnnulationCommercial, 0) = 0
        ORDER BY
            COALESCE(AddedToQueueAt, ParkingAt, CreatedAt) DESC,
            Id DESC;

        ;WITH TargetLegend AS
        (
            SELECT TOP (1) *
            FROM dbo.Ecare_Order_Legend
            WHERE Matricule = @Matricule
              AND ISNULL(Step, 0) = 1
              AND ISNULL(BonDeLivraison, '') = ''
              AND ISNULL(AnnulationCommercial, 0) = 0
            ORDER BY
                COALESCE(AddedToQueueAt, ParkingAt, CreatedAt) DESC,
                Id DESC
        )
        UPDATE TargetLegend
        SET
            IsPined = @ShouldPin,
            PinedAt = CASE WHEN @ShouldPin = 1 THEN @Now ELSE NULL END;

        DECLARE @LegendRows INT = @@ROWCOUNT;

        UPDATE q
        SET
            IsPined = @ShouldPin,
            PinedAt = CASE WHEN @ShouldPin = 1 THEN @Now ELSE NULL END
        FROM dbo.Ecare_Queue q
        WHERE q.Matricule = @Matricule
          AND q.Status BETWEEN 0 AND 1;

        SELECT @LegendRows;";

    public async Task<Result<int>> Handle(TogglePinByMatriculeCommand request, CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            // 1) Flip IsPined; set/clear PinedAt
            var affected = await uow.Connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    ToggleSql,
                    new { request.Matricule },
                    uow.Transaction,
                    cancellationToken: ct));

            await uow.CommitAsync(ct);

            // 2) Rebuild + broadcast snapshot via the shared helper
            await QueueSnapshot.BuildAndBroadcastAsync(signalR, uow, log, ct);

            log.LogInformation("Toggled pin for Matricule={Matricule}, affected={Count}", request.Matricule, affected);
            return Result<int>.Ok(affected);
        }
        catch (Exception ex)
        {
            try { await uow.RollbackAsync(ct); } catch { }
            log.LogError(ex, "Failed to toggle pin for Matricule={Matricule}", request.Matricule);
            throw;
        }
    }
}
