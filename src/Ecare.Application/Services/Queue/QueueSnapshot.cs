using Dapper;
using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Ecare.Application.Services
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

        private const string CapacitySql = @"
        SELECT  c.Name AS Quality,
                COALESCE(SUM(l.Capacity), 0) AS Capacity
        FROM    dbo.Ecare_Ligne          AS l
        JOIN    dbo.Ecare_LigneCiments   AS lc ON lc.LigneId = l.Id
        JOIN    dbo.EcareCiments         AS c  ON c.Id = lc.CimentId
        WHERE   c.Name IN @Qualities
        GROUP BY c.Name;";

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

                // Distinct non-empty qualities for Status=1 groups
                var qualities = rows
                    .Where(i => i.Status == QueueStatus.EncourTraitement && !string.IsNullOrWhiteSpace(i.Qualite1))
                    .Select(i => i.Qualite1!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Fetch total capacity per quality (product) in the SAME transaction
                var capRows = qualities.Count == 0
                    ? Array.Empty<(string Quality, int Capacity)>()
                    : (await uow.Connection.QueryAsync<(string Quality, int Capacity)>(
                        new CommandDefinition(CapacitySql, new { Qualities = qualities }, transaction: uow.Transaction, cancellationToken: ct)
                      )).ToArray();

                var caps = capRows.ToDictionary(
                    k => k.Quality,
                    v => v.Capacity,
                    StringComparer.OrdinalIgnoreCase);

                await uow.CommitAsync(ct);

                // 0 = EnValidation: FIFO
                var enValidation = rows
                    .Where(i => i.Status == QueueStatus.EnValidation)
                    .OrderBy(i => i.CreatedAt)
                    .ToList();

                // 1 = EncourTraitement: group by Qualite1; compute Capacity per group
                var inProgressGroups = rows
                    .Where(i => i.Status == QueueStatus.EncourTraitement)
                    .GroupBy(i => string.IsNullOrWhiteSpace(i.Qualite1) ? "(Sans Qualité)" : i.Qualite1!.Trim(),
                             StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g.Key) // alphabetical headers
                    .Select(g =>
                    {
                        var items = g.OrderByDescending(i => i.IsPined)
                                     .ThenByDescending(i => i.PinedAt ?? DateTime.MinValue)
                                     .ThenByDescending(i => i.CreatedAt)
                                     .ToList();

                        // Capacity lookup (0 if product isn’t mapped to any line)
                        var capacity = caps.TryGetValue(g.Key, out var cap) ? cap : 0;

                        return new QueueGroupDto(
                            Name: g.Key,
                            Items: items,
                            Capacity: capacity
                        );
                    })
                    .ToList();

                var result = new List<QueueGroupDto>(1 + inProgressGroups.Count);
                if (enValidation.Count > 0)
                    result.Add(new QueueGroupDto("En Validation", enValidation, 0)); // capacity not meaningful here

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
