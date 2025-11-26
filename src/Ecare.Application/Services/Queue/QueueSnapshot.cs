using Dapper;
using Ecare.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Ecare.Application.Services;

public static class QueueSnapshot
{
    private const string HubName = "queue_data_hub";
    private const string MethodName = "QueueDataEvent";

    public sealed record LegendRow(
        int Id,
        int? OrderId,
        string ClientName,
        string Chantier,
        string Matricule,
        int RFIDCard,
        string TypeCamion,
        int? NombrePlombs,
        string? Produit1,
        int? Quantite1,
        bool IsPined,
        DateTime? PinedAt,
        DateTime AddedToQueueAt,
        DateTime? FirstPlaceAt,
        decimal? TimeElapsedInFirstPlace,
        int Step,
        string TruckType,
        DateTime ParkingAt
    );

    public sealed record QueueItem(
        string Matricule,
        string? Produit1,
        bool IsPined,
        DateTime? PinedAt,
        DateTime AddedToQueueAt,
        string TruckType
    );

    public sealed record QueueGroup(
        string Name,
        IReadOnlyList<QueueItem> Items,
        int Capacity
    );

    /* ============================================================
       MAIN ENTRY — BUILD SNAPSHOT
       ============================================================ */
    public static async Task<IReadOnlyList<QueueGroup>> BuildAsync(
        IUnitOfWork uow, ILogger? log = null, CancellationToken ct = default)
    {
        await uow.BeginAsync(ct);

        // 1) Load from stored procedure
        var rows = (await uow.Connection.QueryAsync<LegendRow>(
            new CommandDefinition("sp_GetQueueSnapshotLegend",
                                  transaction: uow.Transaction,
                                  cancellationToken: ct,
                                  commandType: System.Data.CommandType.StoredProcedure)))
            .ToList();

        /* ============================================================
           GROUPING LOGIC
           ============================================================ */

        // EN VALIDATION VRAC (TruckType = Citerne AND no Produit1)
        var enValidationVrac = rows
            .Where(r => r.Produit1 is null &&
                        r.TruckType.Equals("Citerne", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.IsPined)
            .ThenByDescending(r => r.PinedAt ?? DateTime.MinValue)
            .ThenBy(r => r.AddedToQueueAt)
            .Select(r => new QueueItem(r.Matricule, null, r.IsPined, r.PinedAt, r.AddedToQueueAt, r.TruckType))
            .ToList();

        // EN VALIDATION SAC (TruckType ≠ Citerne AND no Produit1)
        var enValidationSac = rows
            .Where(r => r.Produit1 is null &&
                        !r.TruckType.Equals("Citerne", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.IsPined)
            .ThenByDescending(r => r.PinedAt ?? DateTime.MinValue)
            .ThenBy(r => r.AddedToQueueAt)
            .Select(r => new QueueItem(r.Matricule, null, r.IsPined, r.PinedAt, r.AddedToQueueAt, r.TruckType))
            .ToList();

        /* ============================================================
           PROGRESS GROUPS (Produit1 NOT NULL)
           ============================================================ */
        var progressGroups =
            rows.Where(r => r.Produit1 != null)
                .GroupBy(r => r.Produit1!.Trim())
                .Select(g =>
                {
                    var items = g.OrderByDescending(r => r.IsPined)
                                 .ThenByDescending(r => r.PinedAt ?? DateTime.MinValue)
                                 .ThenBy(r => r.AddedToQueueAt)
                                 .Select(r => new QueueItem(r.Matricule, r.Produit1, r.IsPined, r.PinedAt, r.AddedToQueueAt, r.TruckType))
                                 .ToList();

                    // Compute capacity per product
                    int cap = CapacityCache.GetCapacity(g.Key, uow);

                    return new QueueGroup(g.Key, items, cap);
                })
                .ToList();

        await uow.CommitAsync(ct);

        // Build final list
        var groups = new List<QueueGroup>();

        if (enValidationVrac.Count > 0)
            groups.Add(new QueueGroup("En Validation VRAC", enValidationVrac, 0));

        if (enValidationSac.Count > 0)
            groups.Add(new QueueGroup("En Validation SAC", enValidationSac, 0));

        groups.AddRange(progressGroups);

        // Logging
        log?.LogInformation("Legend snapshot built: {groups} groups, {items} items",
            groups.Count, groups.Sum(x => x.Items.Count));

        return groups;
    }

    /* ============================================================
       BROADCAST
       ============================================================ */
    public static async Task BroadcastAsync(
        ServiceManager manager, IEnumerable<QueueGroup> snapshot, ILogger? log = null, CancellationToken ct = default)
    {
        await using var hub = await manager.CreateHubContextAsync(HubName, ct);
        await hub.Clients.All.SendAsync(MethodName, snapshot, ct);

        log?.LogInformation("Queue snapshot broadcasted to {Hub}", HubName);
    }

    public static async Task<IReadOnlyList<QueueGroup>> BuildAndBroadcastAsync(
        ServiceManager manager, IUnitOfWork uow, ILogger? log = null, CancellationToken ct = default)
    {
        var snap = await BuildAsync(uow, log, ct);
        await BroadcastAsync(manager, snap, log, ct);
        return snap;
    }


    public static class CapacityCache
    {
        public static int GetCapacity(string product, IUnitOfWork uow)
        {
            const string sql = @"
            SELECT COALESCE(SUM(l.Capacity),0)
            FROM dbo.Ecare_Ligne l
            JOIN dbo.Ecare_LigneCiments lc ON lc.LigneId = l.Id
            JOIN dbo.EcareCiments c ON c.Id = lc.CimentId
            WHERE c.Name = @Product;
        ";

            return uow.Connection.ExecuteScalar<int>(
                sql, new { Product = product }, uow.Transaction);
        }
    }

}
