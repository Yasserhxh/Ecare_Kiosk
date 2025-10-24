// Ecare.Application/Commands/Queue/TogglePin/TogglePinByMatriculeHandler.cs
using Dapper;
using Ecare.Application.Commands.Queue.PinTruck;
using Ecare.Application.Services;
using Ecare.Domain.ValueObjects; // contains QueueStatus enum (EnValidation = 0, EncourTraitement = 1)
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
    // We read IsPined + PinedAt to order properly (but ignore for EnValidation).
    private const string SelectSql = @"
        SELECT 
            Matricule,
            Qualite1,
            Status,
            CreatedAt,
            IsPined,
            PinedAt
        FROM dbo.Ecare_Queue
        WHERE Status BETWEEN 0 AND 1
        ORDER BY CreatedAt ASC;";

    // Toggle for all active rows for the given matricule (0/1 statuses).
    // If you want only the latest row, wrap with TOP(1) on a CTE.
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
            var affected = await uow.Connection.ExecuteAsync(
                new CommandDefinition(ToggleSql, new { request.Matricule }, uow.Transaction, cancellationToken: ct));

            await uow.CommitAsync(ct);

            // Rebuild snapshot (pinned ignored in EnValidation, used in in-progress)
            var grouped = await BuildGroupedSnapshotAsync(ct);

            // Broadcast refresh to all clients
            await SignalRHelper.BroadcastAsync(
                signalR,
                hubName: "queue_data_hub",
                methodName: "QueueDataEvent",
                payload: grouped,
                ct: ct,
                logger: log);

            log.LogInformation("Toggled pin for Matricule={matricule}, affected={count}", request.Matricule, affected);
            return Result<int>.Ok(affected);
        }
        catch (Exception ex)
        {
            try { await uow.RollbackAsync(ct); } catch { }
            log.LogError(ex, "Failed to toggle pin for Matricule={m}", request.Matricule);
            throw;
        }
    }

    // Snapshot models
    private sealed record QueueItem(
        string? Matricule,
        string? Qualite1,
        QueueStatus Status,
        DateTime CreatedAt,
        bool IsPined,
        DateTime? PinedAt);

    private sealed record QueueGroup(
        string Name,
        IReadOnlyList<QueueItem> Items);

    private async Task<IReadOnlyList<QueueGroup>> BuildGroupedSnapshotAsync(CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            var items = (await uow.Connection.QueryAsync<QueueItem>(
                new CommandDefinition(SelectSql, transaction: uow.Transaction, cancellationToken: ct))).ToList();

            await uow.CommitAsync(ct);

            // Status = 0 → "En Validation" (IGNORE pinning)
            var enValidation = items
                .Where(i => i.Status == QueueStatus.EnValidation)
                .OrderBy(i => i.CreatedAt)
                .ToList();

            // Status = 1 → group by Qualite1, pinned-first in each group
            var inProgressGroups = items
                .Where(i => i.Status == QueueStatus.EncourTraitement)
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Qualite1) ? "(Sans Qualité)" : i.Qualite1!)
                .OrderBy(g => g.Key)
                .Select(g => new QueueGroup(
                    Name: g.Key,
                    Items: g
                        .OrderByDescending(i => i.IsPined)                       // pinned first
                        .ThenByDescending(i => i.PinedAt ?? DateTime.MinValue)   // newest pin higher
                        .ThenBy(i => i.CreatedAt)                                // then FIFO
                        .ToList()))
                .ToList();

            var result = new List<QueueGroup>(1 + inProgressGroups.Count);
            if (enValidation.Count > 0)
                result.Add(new QueueGroup("En Validation", enValidation));

            result.AddRange(inProgressGroups);
            return result;
        }
        catch
        {
            try { await uow.RollbackAsync(ct); } catch { }
            throw;
        }
    }
}
