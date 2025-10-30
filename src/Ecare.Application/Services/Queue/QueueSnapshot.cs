using Dapper;
using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Ecare.Application.Services.Queue
{
    /// <summary>
    /// Builds the grouped queue snapshot from dbo.Ecare_Queue and
    /// broadcasts it as QueueDataEvent on queue_data_hub.
    /// </summary>
    public static class QueueSnapshot
    {
        private const string HubName = "queue_data_hub";
        private const string MethodName = "QueueDataEvent";

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
ORDER BY CreatedAt ASC; -- base order (oldest → newest)
";

        /// <summary>
        /// Reads from Ecare_Queue and returns grouped snapshot with ordering rules:
        /// - Status=0 (EnValidation): FIFO by CreatedAt
        /// - Status=1 (EncourTraitement): group by Qualite1; within group, pinned first (IsPined desc),
        ///   then most recently pinned (PinedAt desc), then FIFO by CreatedAt.
        /// </summary>
        public static async Task<IReadOnlyList<QueueGroupDto>> BuildAsync(IUnitOfWork uow, ILogger? log = null, CancellationToken ct = default)
        {
            await uow.BeginAsync(ct);
            try
            {
                var rows = (await uow.Connection.QueryAsync<QueueItemDto>(
                    new CommandDefinition(SelectSql, transaction: uow.Transaction, cancellationToken: ct)
                )).ToList();

                await uow.CommitAsync(ct);

                // 0 = EnValidation => ignore pinning (pure FIFO)
                var enValidation = rows
                    .Where(i => i.Status == QueueStatus.EnValidation)
                    .OrderBy(i => i.CreatedAt)
                    .ToList();

                // 1 = EncourTraitement => group by Qualite1; pinned first
                var inProgressGroups = rows
                    .Where(i => i.Status == QueueStatus.EncourTraitement)
                    .GroupBy(i => string.IsNullOrWhiteSpace(i.Qualite1) ? "(Sans Qualité)" : i.Qualite1!)
                    .OrderBy(g => g.Key) // alphabetical group header
                    .Select(g => new QueueGroupDto(
                        Name: g.Key,
                        Items: g
                            .OrderByDescending(i => i.IsPined)                        // pinned first
                            .ThenByDescending(i => i.PinedAt ?? DateTime.MinValue)    // newest pin first
                            .ThenBy(i => i.CreatedAt)                                  // then FIFO
                            .ToList()
                    ))
                    .ToList();

                var result = new List<QueueGroupDto>(1 + inProgressGroups.Count);
                if (enValidation.Count > 0)
                    result.Add(new QueueGroupDto("En Validation", enValidation));

                result.AddRange(inProgressGroups);

                log?.LogInformation("Built queue snapshot: {groups} groups, {items} items",
                    result.Count, result.Sum(g => g.Items.Count));

                return result;
            }
            catch
            {
                try { await uow.RollbackAsync(ct); } catch { }
                throw;
            }
        }

        /// <summary>
        /// Broadcasts an already built snapshot to queue_data_hub -> QueueDataEvent.
        /// </summary>
        public static async Task BroadcastAsync(ServiceManager manager, IEnumerable<QueueGroupDto> snapshot, ILogger? log = null, CancellationToken ct = default)
        {
            await using var hub = await manager.CreateHubContextAsync(HubName, ct);
            await hub.Clients.All.SendAsync(MethodName, snapshot, ct);

            if (log != null)
            {
                try
                {
                    var json = JsonSerializer.Serialize(snapshot);
                    log.LogInformation("Broadcasted {Method} to {Hub}. Payload size ~{len} chars.",
                        MethodName, HubName, json.Length);
                }
                catch
                {
                    log.LogInformation("Broadcasted {Method} to {Hub}.", MethodName, HubName);
                }
            }
        }

        /// <summary>
        /// Convenience: build and broadcast in one call. Returns the built snapshot.
        /// </summary>
        public static async Task<IReadOnlyList<QueueGroupDto>> BuildAndBroadcastAsync(
            ServiceManager manager,
            IUnitOfWork uow,
            ILogger? log = null,
            CancellationToken ct = default)
        {
            var snapshot = await BuildAsync(uow, log, ct);
            await BroadcastAsync(manager, snapshot, log, ct);
            return snapshot;
        }
    }
}
