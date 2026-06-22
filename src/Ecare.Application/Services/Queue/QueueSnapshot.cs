using Dapper;
using Ecare.Shared;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Ecare.Application.Services;

public static class QueueSnapshot
{
    private const string HubName = "queue_data_hub";
    private const string MethodName = "QueueDataEvent";

    public sealed class LegendRow
    {
        public int Id { get; set; }
        public int? OrderId { get; set; }
        public string ClientName { get; set; }
        public string Chantier { get; set; }
        public string Matricule { get; set; }
        public string RFIDCard { get; set; }
        public string? TypeCamion { get; set; }
        public int? NombrePlombs { get; set; }
        public string? Produit1 { get; set; }
        public double? Quantite1 { get; set; }
        public bool IsPined { get; set; }
        public DateTime? PinedAt { get; set; }
        public DateTime AddedToQueueAt { get; set; }
        public DateTime? FirstPlaceAt { get; set; }
        public decimal? TimeElapsedInFirstPlace { get; set; }
        public int Step { get; set; }
        public string? TruckType { get; set; }
        public DateTime ParkingAt { get; set; }
        public string ChauffeurName { get; set; }
        public string? TypeProduit {  get; set; }

        public LegendRow() { } 
    }


    public sealed record QueueItem(
        string Matricule,
        string? Produit1,
        bool IsPined,
        DateTime? PinedAt,
        DateTime AddedToQueueAt,
        DateTime? FirstPlaceAt,
        decimal? TimeElapsedInFirstPlace,
        string TruckType,
        string chauffeurNom,
        string? TypeProduit
    );

    public sealed record QueueGroup(
        string Name,
        IReadOnlyList<QueueItem> Items,
        int Capacity
    );

    public sealed record FirstWeightEligibilityResult(
        bool IsAllowed,
        string Reason,
        string GroupName,
        int Capacity,
        int Position,
        bool IsPalGroup
    );

    private static bool IsPalRow(LegendRow row)
    {
        var typeProduit = (row.TypeProduit ?? string.Empty).Trim();
        if (typeProduit.Equals("PAL", StringComparison.OrdinalIgnoreCase))
            return true;

        var produit = (row.Produit1 ?? string.Empty).Trim();
        return produit.Contains("PAL", StringComparison.OrdinalIgnoreCase)
            || produit.Contains("PALET", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime ResolveQueueTimestamp(LegendRow row)
    {
        if (row.FirstPlaceAt.HasValue)
            return row.FirstPlaceAt.Value;

        if (row.AddedToQueueAt != default)
            return row.AddedToQueueAt;

        if (row.ParkingAt != default)
            return row.ParkingAt;

        return DateTime.MaxValue;
    }

    /// <summary>
    /// Canonical queue ordering, shared by every list and aligned with the calling order:
    /// pinned first, earliest pin first, then FIFO by resolved queue timestamp (unset → last),
    /// then Id for stability. Replaces the previously divergent VRAC/SAC/progress sorts.
    /// </summary>
    public static IEnumerable<LegendRow> OrderForQueue(IEnumerable<LegendRow> rows) =>
        rows.OrderByDescending(r => r.IsPined)
            .ThenBy(r => r.PinedAt ?? DateTime.MaxValue)
            .ThenBy(ResolveQueueTimestamp)
            .ThenBy(r => r.Id);

    private static bool IsFirstPlaceExpired(LegendRow row, DateTime now)
    {
        return row.FirstPlaceAt.HasValue
            && now - row.FirstPlaceAt.Value >= TimeSpan.FromHours(1);
    }

    /* ============================================================
       MAIN ENTRY — BUILD SNAPSHOT
       ============================================================ */
    public static async Task<IReadOnlyList<QueueGroup>> BuildAsync(
        IUnitOfWork uow, ILogger? log = null, CancellationToken ct = default)
    {
        await uow.BeginAsync(ct);
        _ = Task.Run(async () =>
        {
            try
            {
                

                await uow.Connection.ExecuteAsync(
                    "sp_UpdateAddedToQueueAfterFirstPlace",
                    commandType: CommandType.StoredProcedure);
            }
            catch (Exception ex)
            {
                log?.LogError(ex, "Background update AddedToQueueAt failed");
            }
        });



        

       


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
        var enValidationVrac = OrderForQueue(rows
            .Where(r =>
                r.Produit1 is null &&
                r.TruckType != null &&
                r.TruckType.Equals("Citerne", StringComparison.OrdinalIgnoreCase)))
            .Select(r => new QueueItem(
                r.Matricule,
                null,
                r.IsPined,
                r.PinedAt,
                r.AddedToQueueAt,
                r.FirstPlaceAt,
                r.TimeElapsedInFirstPlace,
                r.TruckType,
                r.ChauffeurName,
                r.TypeProduit))
            .ToList();


        // EN VALIDATION SAC (TruckType ≠ Citerne AND no Produit1)
        var enValidationSac = OrderForQueue(rows
            .Where(r => r.Produit1 is null && r.TruckType != null &&
                        !r.TruckType.Equals("Citerne", StringComparison.OrdinalIgnoreCase)))
            .Select(r => new QueueItem(r.Matricule, null, r.IsPined, r.PinedAt, r.AddedToQueueAt, r.FirstPlaceAt, r.TimeElapsedInFirstPlace, r.TruckType, r.ChauffeurName, r.TypeProduit))
            .ToList();

        /* ============================================================
           PROGRESS GROUPS (Produit1 NOT NULL)
           ============================================================ */
        var progressGroups =
            rows.Where(r => !string.IsNullOrWhiteSpace(r.Produit1))
                .GroupBy(r => r.Produit1!.Trim())
                .Select(g =>
                {
                    var items = OrderForQueue(g)
                                 .Select(r => new QueueItem(r.Matricule, r.Produit1, r.IsPined, r.PinedAt, r.AddedToQueueAt, r.FirstPlaceAt, r.TimeElapsedInFirstPlace, r.TruckType, r.ChauffeurName, r.TypeProduit))
                                 .ToList();

                    // Compute capacity per physical loading line/family.
                    int cap = CapacityCache.GetCapacity(g.Key, g.Any(IsPalRow), uow);

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

    public static async Task<FirstWeightEligibilityResult> EvaluateFirstWeightEligibilityAsync(
        IDbConnection connection,
        int legendId,
        IDbTransaction? transaction = null,
        CancellationToken ct = default)
    {
        var rows = (await connection.QueryAsync<LegendRow>(
            new CommandDefinition(
                "sp_GetQueueSnapshotLegend",
                transaction: transaction,
                cancellationToken: ct,
                commandType: CommandType.StoredProcedure)))
            .ToList();

        var target = rows.FirstOrDefault(r => r.Id == legendId);
        if (target is null)
        {
            return new FirstWeightEligibilityResult(
                false, "NO_ACTIVE_ORDER", string.Empty, 0, -1, false);
        }

        if (target.Step != 1 || string.IsNullOrWhiteSpace(target.Produit1))
        {
            return new FirstWeightEligibilityResult(
                false,
                "NOT_READY_FOR_FIRST_WEIGHT",
                target.Produit1?.Trim() ?? string.Empty,
                0,
                -1,
                IsPalRow(target));
        }

        var isPalGroup = IsPalRow(target);
        var groupName = target.Produit1!.Trim();

        var now = DateTime.Now;
        var waitingRows = rows
            .Where(r =>
                r.Step == 1 &&
                !string.IsNullOrWhiteSpace(r.Produit1) &&
                (isPalGroup
                    ? IsPalRow(r)
                    : string.Equals(r.Produit1!.Trim(), groupName, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(r => IsFirstPlaceExpired(r, now) ? 1 : 0)
            .ThenByDescending(r => r.IsPined)
            .ThenBy(r => r.IsPined ? (r.PinedAt ?? DateTime.MaxValue) : DateTime.MaxValue)
            .ThenBy(ResolveQueueTimestamp)
            .ThenBy(r => r.Id)
            .ToList();

        var capacity = await CapacityCache.GetCapacityAsync(
            connection,
            groupName,
            isPalGroup,
            transaction,
            ct);

        var position = waitingRows.FindIndex(r => r.Id == legendId) + 1;
        if (target.IsPined)
        {
            return new FirstWeightEligibilityResult(
                true,
                "FORCE_CALL",
                groupName,
                capacity,
                position,
                isPalGroup);
        }

        if (capacity <= 0)
        {
            return new FirstWeightEligibilityResult(
                false,
                "LINE_HAS_NO_CAPACITY",
                groupName,
                capacity,
                position,
                isPalGroup);
        }

        if (target.FirstPlaceAt.HasValue && !IsFirstPlaceExpired(target, now))
        {
            return new FirstWeightEligibilityResult(
                true,
                "OK",
                groupName,
                capacity,
                position,
                isPalGroup);
        }

        var allowedIds = waitingRows.Take(capacity).Select(r => r.Id).ToHashSet();
        var isAllowed = allowedIds.Contains(legendId);

        return new FirstWeightEligibilityResult(
            isAllowed,
            isAllowed ? "OK" : "NOT_CALLED_YET",
            groupName,
            capacity,
            position,
            isPalGroup);
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
        public static int GetCapacity(string groupKey, bool isPalGroup, IUnitOfWork uow)
        {
            const string sqlProduct = @"
                SELECT 
                    COALESCE(SUM(EL.RealtimeCapacity), 0) AS TotalRealtimeCapacity
                FROM Ecare_Ligne AS EL
                JOIN Ecare_LigneCiments AS LC ON LC.LigneId = EL.Id
                JOIN EcareCiments AS C ON C.Id = LC.CimentId
                WHERE LC.Actif = 1
                  AND ISNULL(EL.Status, 0) = 1
                  AND C.Name = @GroupKey;";

            const string sqlPal = @"
                SELECT
                    COALESCE(SUM(EL.RealtimeCapacity), 0) AS TotalRealtimeCapacity
                FROM Ecare_Ligne AS EL
                JOIN Ecare_Zone_Chargement AS EZC ON EZC.Id = EL.ZoneChargementId
                JOIN Ecare_LigneCiments AS LC ON LC.LigneId = EL.Id
                JOIN EcareCiments AS C ON C.Id = LC.CimentId
                WHERE LC.Actif = 1
                  AND ISNULL(EL.Status, 0) = 1
                  AND C.Name = @GroupKey
                  AND (
                        UPPER(ISNULL(EZC.TypeOperation, '')) = 'PAL'
                        OR UPPER(ISNULL(EZC.TypeActivite, '')) = 'PAL'
                        OR UPPER(ISNULL(C.[Type], '')) = 'PAL'
                      );";

            return uow.Connection.ExecuteScalar<int>(
                isPalGroup ? sqlPal : sqlProduct,
                new { GroupKey = groupKey },
                uow.Transaction
            );
        }

        public static Task<int> GetCapacityAsync(
            IDbConnection connection,
            string groupKey,
            bool isPalGroup,
            IDbTransaction? transaction = null,
            CancellationToken ct = default)
        {
            const string sqlProduct = @"
                SELECT 
                    COALESCE(SUM(EL.RealtimeCapacity), 0) AS TotalRealtimeCapacity
                FROM Ecare_Ligne AS EL
                JOIN Ecare_LigneCiments AS LC ON LC.LigneId = EL.Id
                JOIN EcareCiments AS C ON C.Id = LC.CimentId
                WHERE LC.Actif = 1
                  AND ISNULL(EL.Status, 0) = 1
                  AND C.Name = @GroupKey;";

            const string sqlPal = @"
                SELECT
                    COALESCE(SUM(EL.RealtimeCapacity), 0) AS TotalRealtimeCapacity
                FROM Ecare_Ligne AS EL
                JOIN Ecare_Zone_Chargement AS EZC ON EZC.Id = EL.ZoneChargementId
                JOIN Ecare_LigneCiments AS LC ON LC.LigneId = EL.Id
                JOIN EcareCiments AS C ON C.Id = LC.CimentId
                WHERE LC.Actif = 1
                  AND ISNULL(EL.Status, 0) = 1
                  AND C.Name = @GroupKey
                  AND (
                        UPPER(ISNULL(EZC.TypeOperation, '')) = 'PAL'
                        OR UPPER(ISNULL(EZC.TypeActivite, '')) = 'PAL'
                        OR UPPER(ISNULL(C.[Type], '')) = 'PAL'
                      );";

            return connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    isPalGroup ? sqlPal : sqlProduct,
                    new { GroupKey = groupKey },
                    transaction,
                    cancellationToken: ct));
        }
    }

}
