using Dapper;
using Ecare.Application.Services.Queue;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Commands.Queue.PinTruck;

public sealed class TogglePinByMatriculeHandler(
    IUnitOfWork uow,
    ServiceManager signalR,
    ILogger<TogglePinByMatriculeHandler> log)
    : IRequestHandler<TogglePinByMatriculeCommand, Result<int>>
{
    // Toggle pin for all active rows (Status 0 or 1) for the given matricule.
    // If you want only the most-recent row, add TOP(1) logic with an ordered CTE.
    private const string ToggleSql = @"
        UPDATE q
        SET 
            IsPined = CASE WHEN IsPined = 1 THEN 0 ELSE 1 END,
            PinedAt = CASE WHEN IsPined = 1 THEN NULL ELSE SYSUTCDATETIME() END
        FROM dbo.Ecare_Queue q
        WHERE q.Matricule = @Matricule
          AND q.Status BETWEEN 0 AND 1;";

    public async Task<Result<int>> Handle(TogglePinByMatriculeCommand request, CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            // 1) Flip IsPined; set/clear PinedAt
            var affected = await uow.Connection.ExecuteAsync(
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
