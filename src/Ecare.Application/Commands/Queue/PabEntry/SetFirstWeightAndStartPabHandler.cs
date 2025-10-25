using Dapper;
using Ecare.Application.Services;
using Ecare.Domain.ValueObjects; // QueueStatus (EnValidation=0, EncourTraitement=1)
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Linq;

namespace Ecare.Application.Commands.Queue.PabEntry;

public sealed class SetFirstWeightAndStartPabHandler(
    IUnitOfWork uow,
    ServiceManager signalR,
    ILogger<SetFirstWeightAndStartPabHandler> log
) : IRequestHandler<SetFirstWeightAndStartPabCommand, Result<int>>
{
    // We include IsPined/PinedAt so we can order properly when rebuilding the snapshot.
    private const string SnapshotSql = @"
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

    // Update the *latest* active queue row (0/1) for the given Matricule to Status=2.
    private const string UpdateQueueSql = @"
        UPDATE q SET q.Status = 2
        FROM dbo.Ecare_Queue q
        WHERE q.Id = (
            SELECT TOP (1) Id
            FROM dbo.Ecare_Queue
            WHERE Matricule = @Matricule AND Status BETWEEN 0 AND 1
            ORDER BY CreatedAt DESC
        );";

    // Set FirstWeight + PabEntryAt; if no row exists for this Matricule (with PabEntryAt NULL), insert a minimal row.
    private const string UpsertFluxSql = @"
        UPDATE f
        SET 
            FirstWeight = @FirstWeight,
            PabEntryAt = SYSUTCDATETIME(),
            OrderId     = COALESCE(@OrderId, OrderId)
        FROM dbo.EcareFlux f
        WHERE f.Matricule = @Matricule
          AND f.PabEntryAt IS NULL;

        IF @@ROWCOUNT = 0
        BEGIN
            INSERT INTO dbo.EcareFlux
                (BonDeCommande, Quantity, Matricule, DriverName, ClientName, ParkedAt, FirstWeight, PabEntryAt, OrderId)
            VALUES
                (NULL,          NULL,     @Matricule, NULL,       NULL,        NULL,      @FirstWeight, SYSUTCDATETIME(), @OrderId);
        END
    ";

    public async Task<Result<int>> Handle(SetFirstWeightAndStartPabCommand cmd, CancellationToken ct)
    {
        // 1) Flip queue status to 2 for the latest row of this Matricule
        await uow.BeginAsync(ct);
        try
        {
            var affectedQueue = await uow.Connection.ExecuteAsync(
                new CommandDefinition(UpdateQueueSql, new { cmd.Matricule }, uow.Transaction, cancellationToken: ct));

            // We still proceed even if 0 rows (maybe already 2), but we log it.
            if (affectedQueue == 0)
                log.LogWarning("No active queue row (0/1) found to set Status=2 for Matricule={m}", cmd.Matricule);

            // 2) Upsert Flux: FirstWeight + PabEntryAt (UTC now)
            var p = new DynamicParameters();
            p.Add("Matricule", cmd.Matricule, DbType.String);
            p.Add("FirstWeight", cmd.FirstWeight, DbType.Int32);
            p.Add("OrderId", cmd.OrderId, DbType.Int32);

            await uow.Connection.ExecuteAsync(
                new CommandDefinition(UpsertFluxSql, p, uow.Transaction, cancellationToken: ct));

            await uow.CommitAsync(ct);
        }
        catch
        {
            try { await uow.RollbackAsync(ct); } catch { }
            throw;
        }

        // 3) Rebuild grouped snapshot for Status 0 + 1 and broadcast
        var grouped = await BuildGroupedSnapshotAsync(ct);
        await SignalRHelper.BroadcastAsync(
            signalR,
            hubName: "queue_data_hub",
            methodName: "QueueDataEvent",
            payload: grouped,
            logger: log,
            ct: ct
        );

        log.LogInformation("PAB entry started for Matricule={m}; FirstWeight={w}; snapshot broadcasted.",
            cmd.Matricule, cmd.FirstWeight);

        // Return how many queue rows were updated to 2 (0 or 1 by our query)
        return Result<int>.Ok(1);
    }

    // --- snapshot models (same shape you already used on frontend) ---
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
                new CommandDefinition(SnapshotSql, transaction: uow.Transaction, cancellationToken: ct)
            )).ToList();

            await uow.CommitAsync(ct);

            // Lane 1: En Validation (0) — ignore pin; oldest first
            var enValidation = items
                .Where(i => i.Status == QueueStatus.EnValidation)
                .OrderBy(i => i.CreatedAt)
                .ToList();

            // Lane 2: En cours de traitement (1) — group by Qualite1, pinned first
            var inProgressGroups = items
                .Where(i => i.Status == QueueStatus.EncourTraitement)
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Qualite1) ? "(Sans Qualité)" : i.Qualite1!)
                .OrderBy(g => g.Key)
                .Select(g => new QueueGroup(
                    Name: g.Key,
                    Items: g
                        .OrderByDescending(i => i.IsPined)
                        .ThenByDescending(i => i.PinedAt ?? DateTime.MinValue)
                        .ThenBy(i => i.CreatedAt)
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
