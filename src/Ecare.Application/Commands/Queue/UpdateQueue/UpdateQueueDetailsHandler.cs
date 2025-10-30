// Ecare.Application/Commands/Queue/UpdateQueue/UpdateQueueDetailsHandler.cs
using System.Data;
using Dapper;
using Ecare.Domain.ValueObjects; // QueueStatus enum (EnValidation=0, EncourTraitement=1)
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Commands.Queue.UpdateQueue;

public sealed class UpdateQueueDetailsHandler(
    IUnitOfWork uow,
    ServiceManager signalR,
    ILogger<UpdateQueueDetailsHandler> log)
    : IRequestHandler<UpdateQueueDetailsCommand, Result<int>>
{
    // Update target row(s) for this Matricule that are active (0/1).
    private const string UpdateSql = @"
UPDATE q
SET
    Bon_Commande = @BonCommande,
    Qualite1     = @Qualite1,
    Qualite2     = @Qualite2,
    Quantite1    = @Quantite1,
    Quantite2    = @Quantite2,
    Nom_Chaufeur = @NomChauffeur,
    Status       = 1          -- promote to 'En cours de traitement'
FROM dbo.Ecare_Queue q
WHERE q.Matricule = @Matricule
  AND q.Status BETWEEN 0 AND 1;";

    // Include pin fields for correct ordering (ignored for EnValidation).
    private const string SelectSql = @"
SELECT Matricule, Qualite1, Status, CreatedAt, IsPined, PinedAt
FROM dbo.Ecare_Queue
WHERE Status BETWEEN 0 AND 1;";

    public async Task<Result<int>> Handle(UpdateQueueDetailsCommand request, CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            var affected = await uow.Connection.ExecuteAsync(
                new CommandDefinition(
                    UpdateSql,
                    new
                    {
                        request.Matricule,
                        request.BonCommande,
                        request.Qualite1,
                        request.Qualite2,
                        request.Quantite1,
                        request.Quantite2,
                        NomChauffeur = request.NomChauffeur
                    },
                    uow.Transaction,
                    cancellationToken: ct));

            await uow.CommitAsync(ct);

            // Rebuild grouped snapshot
            var grouped = await BuildGroupedSnapshotAsync(ct);

            // Broadcast to SignalR
            await SignalRHelper.BroadcastAsync(
                signalR,
                hubName: "queue_data_hub",
                methodName: "QueueDataEvent",
                payload: grouped,
                ct: ct,
                logger: log);

            log.LogInformation("Queue updated for Matricule={m}, affected={n}", request.Matricule, affected);
            return Result<int>.Ok(affected);
        }
        catch
        {
            try { await uow.RollbackAsync(ct); } catch { }
            throw;
        }
    }

    // ----- snapshot (same rules as earlier) -----
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

            var enValidation = items
                .Where(i => i.Status == QueueStatus.EnValidation)
                .OrderBy(i => i.CreatedAt)
                .ToList();

            var inProgress = items
                .Where(i => i.Status == QueueStatus.EncourTraitement)
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Qualite1) ? "(Sans Qualité)" : i.Qualite1!)
                .OrderBy(g => g.Key)
                .Select(g => new QueueGroup(
                    g.Key,
                    g.OrderByDescending(i => i.IsPined)
                     .ThenByDescending(i => i.PinedAt ?? DateTime.MinValue)
                     .ThenBy(i => i.CreatedAt)
                     .ToList()))
                .ToList();

            var result = new List<QueueGroup>();
            if (enValidation.Count > 0)
                result.Add(new QueueGroup("En Validation", enValidation));
            result.AddRange(inProgress);
            return result;
        }
        catch
        {
            try { await uow.RollbackAsync(ct); } catch { }
            throw;
        }
    }
}
