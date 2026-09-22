using Dapper;
using Ecare.Application.Common;
using Ecare.Application.Dtos;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;

namespace Ecare.Application.Commands.Legend;

#region Options

public sealed class PrintOutboundOptions
{
    public string Hub { get; set; } = "print_data_hub";
    public string Method { get; set; } = "PrintDataEvent";
}

#endregion

#region SAP Shipment DTOs

public sealed class ShipmentNotificationRequest
{
    public int Id { get; set; }
}

public sealed class TestWeightDto
{
    public int Id { get; set; }
    public int? OrderId { get; set; }
    public string? Ligne { get; set; }
    public string? TypeProduit { get; set; }
    public int? PremierePoid { get; set; }
    public decimal? Quantite1 { get; set; }
    public decimal? Quantite2 { get; set; }
    public int? PTAC { get; set; }
    public int? Step { get; set; }
    public DateTime? FinishedChargingAt { get; set; }
    public DateTime? PabExitAt { get; set; }
    // Offline flags
    public bool IsOffline { get; set; }
    public string? OfflineStatus { get; set; }
}

public sealed class BlJson
{
    public string? Site { get; set; }
    public string? BonDeLivraison { get; set; }
    // ID interne MyCimar (CommercialOrder), relayé de l'API SAP vers PrintProgramm.
    public int? IdMyCimar { get; set; }
    public ClientJson? Client { get; set; }
    public TransportJson? Transport { get; set; }
    public PesageJson? Pesage { get; set; }
    public List<ProductJson>? Produits { get; set; }
}
public sealed class ClientJson
{
    public string? CodeSap { get; set; }
    public string? Name { get; set; }
    public string? Chantier { get; set; }
    public string? BonDeCommande { get; set; }
}
public sealed class TransportJson
{
    public string? Transporteur { get; set; }
    public string? Matricule { get; set; }
    public string? Chauffeur { get; set; }
    public string? Cin { get; set; }
    public List<string>? Scelles { get; set; }
}
public sealed class PesageJson
{
    public string? PoidsVide { get; set; }
    public string? PoidsBrut { get; set; }
    public DateTime? PabEntryAt { get; set; }
    public DateTime? PabExitAt { get; set; }
}
public sealed class ProductJson
{
    public string? Code { get; set; }
    public string? Libelle { get; set; }
    public string? Quantite { get; set; }
    public int? Sacs { get; set; }
}

/// <summary>
/// DTO for the offline sync endpoint response (Task 2 web-api).
/// camelCase JSON: { status, codeSapCommande, bonDeLivraison, error }
/// </summary>
internal sealed class SapOfflineSyncResponse
{
    public string? Status { get; set; }
    public string? CodeSapCommande { get; set; }
    public string? BonDeLivraison { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Snapshot of legend data needed to build the provisional BL payload.
/// Populated by a second SELECT after the weight UPDATE.
/// </summary>
public sealed class OfflineLegendSnapshot
{
    public string? Site { get; set; }
    public string? ClientName { get; set; }
    public string? Chantier { get; set; }
    public string? BonDeCommande { get; set; }
    public string? CodeSapClient { get; set; }
    public string? Matricule { get; set; }
    public string? ChauffeurName { get; set; }
    public string? TransporteurName { get; set; }
    public string? PermisDeConduite { get; set; }
    public string? Plombs { get; set; }
    public string? TypeProduit { get; set; }
    public string? CodeSapProduit1 { get; set; }
    public string? CodeSapProduit2 { get; set; }
    public string? Produit1 { get; set; }
    public string? Produit2 { get; set; }
    public decimal? Quantite1 { get; set; }
    public decimal? Quantite2 { get; set; }
    public int? SacNumber { get; set; }
    public int? PremierePoid { get; set; }
    public int? DeuxiemePoid { get; set; }
    public DateTime? PabEntryAt { get; set; }
    public DateTime? PabExitAt { get; set; }
    public int? NumberSacs_Charged { get; set; }
    public int? Weight_Charged { get; set; }
}

#endregion

#region Handler

public sealed class UpdateSecondWeightHandler
    : IRequestHandler<UpdateSecondWeightCommand, Result<UpdateSecondWeightResult>>
{
    private const string SapBaseUrl = "http://app-emea-we-dssprod-dss-001.azurewebsites.net";

    private readonly IConfiguration _cfg;
    private readonly ILogger<UpdateSecondWeightHandler> _log;
    private readonly ServiceManager _signalR;
    private readonly PrintOutboundOptions _opt;
    private readonly HttpClient _http;

    public UpdateSecondWeightHandler(
        IConfiguration cfg,
        ILogger<UpdateSecondWeightHandler> log,
        ServiceManager signalR,
        IOptions<PrintOutboundOptions> opt,
        IHttpClientFactory httpFactory)
    {
        _cfg = cfg;
        _log = log;
        _signalR = signalR;
        _opt = opt.Value;
        _http = httpFactory.CreateClient("SapShipment");
    }

    public async Task<Result<UpdateSecondWeightResult>> Handle(
        UpdateSecondWeightCommand request,
        CancellationToken ct)
    {
        var connStr = _cfg.GetConnectionString("SqlServer");
        if (string.IsNullOrWhiteSpace(connStr))
            return Result<UpdateSecondWeightResult>.Fail("MISSING_SQL_CONNECTION");

        await using var conn = new SqlConnection(connStr);

        var order = await conn.QuerySingleOrDefaultAsync<TestWeightDto>(
            """
            SELECT TOP 1
                Id,
                OrderId,
                Ligne,
                TypeProduit,
                Matricule,
                PremierePoid,
                Quantite1,
                Quantite2,
                PTAC,
                Step,
                FinishedChargingAt,
                PabExitAt,
                ISNULL(IsOffline, 0)    AS IsOffline,
                OfflineStatus
            FROM dbo.Ecare_Order_Legend
            WHERE
                (
                    (
                        @LegendId IS NOT NULL
                        AND Id = @LegendId
                    )
                    OR
                    (
                        LTRIM(RTRIM(CAST(RFIDCard AS NVARCHAR(50)))) = @RFIDCard
                        AND Matricule = @Matricule
                    )
                )
                AND ISNULL(AnnulationCommercial, 0) <> 1
                AND ISNULL(Status, '') <> 'Canceled'
            ORDER BY
                CASE WHEN @LegendId IS NOT NULL AND Id = @LegendId THEN 0 ELSE 1 END,
                CASE WHEN Step BETWEEN 2 AND 4 AND PabExitAt IS NULL THEN 0 ELSE 1 END,
                Id DESC
            """,
            new
            {
                request.LegendId,
                RFIDCard = request.RfidCard?.Trim(),
                request.Matricule
            }
        );

        if (order is null)
            return Result<UpdateSecondWeightResult>.Fail("ORDER_NOT_FOUND");

        if ((order.Step ?? 0) < 2 || (order.Step ?? 0) >= 5 || order.PabExitAt is not null)
        {
            _log.LogWarning(
                "PAB EXIT not ready: ResolvedId={Id} Step={Step} PabExitAt={PabExitAt} " +
                "(requested LegendId={LegendId} RFID={Rfid} Matricule={Matricule})",
                order.Id, order.Step, order.PabExitAt,
                request.LegendId, request.RfidCard, request.Matricule);
            return Result<UpdateSecondWeightResult>.Fail("ORDER_NOT_READY_FOR_EXIT");
        }

        if (!order.PremierePoid.HasValue || request.DeuxiemePoid <= order.PremierePoid.Value)
        {
            await SignalRHelper.BroadcastAsync(
                _signalR,
                "ExitMessageHub",
                "ExitMessageMethod",
                "Not Allowed",
                _log,
                ct);
            return Result<UpdateSecondWeightResult>.Fail("SECOND_WEIGHT_MUST_BE_GREATER_THAN_FIRST");
        }

        var allowedMaxGross = order.PTAC * 1.10m;
        if (order.PTAC.HasValue && request.DeuxiemePoid > allowedMaxGross)
        {
            await SignalRHelper.BroadcastAsync(
                _signalR,
                "ExitMessageHub",
                "ExitMessageMethod",
                "Not Allowed",
                _log,
                ct);
            return Result<UpdateSecondWeightResult>.Fail("GROSS_WEIGHT_OUT_OF_RANGE");
        }

        if (order.TypeProduit is "SAC" or "PAL")
        {
            // Net weight
            var net = request.DeuxiemePoid - order.PremierePoid.Value;

            // Expected weight in kg. Null Quantite2 means simple order, not unknown total.
            var expected = ((order.Quantite1 ?? 0m) + (order.Quantite2 ?? 0m)) * 1000m;

            if (expected <= 0)
            {
                await SignalRHelper.BroadcastAsync(
                    _signalR,
                    "ExitMessageHub",
                    "ExitMessageMethod",
                    "Not Allowed",
                    _log,
                    ct);
                return Result<UpdateSecondWeightResult>.Fail("ORDER_QUANTITY_MISSING");
            }

            // 2% tolerance for SAC/PAL bagged products.
            var tolerance = expected * 0.02m;

            var minAllowed = expected - tolerance;
            var maxAllowed = expected + tolerance;

            // Compare using decimal to avoid rounding surprises
            if ((decimal)net < minAllowed || (decimal)net > maxAllowed)
            {
                await SignalRHelper.BroadcastAsync(
                    _signalR,
                    "ExitMessageHub",
                    "ExitMessageMethod",
                    "Not Allowed",
                    _log,
                    ct);
                return Result<UpdateSecondWeightResult>.Fail("NET_WEIGHT_OUT_OF_RANGE");
            }

        }
        else
        {
            var net = request.DeuxiemePoid - order.PremierePoid.Value;
            var expectedNet = ((order.Quantite1 ?? 0m) + (order.Quantite2 ?? 0m)) * 1000m;
            var netTolerance = expectedNet * 0.08m;
            var minAllowedNet = expectedNet - netTolerance;
            var maxAllowedNet = expectedNet + netTolerance;

            if ((decimal)net < minAllowedNet || (decimal)net > maxAllowedNet)
            {
                await SignalRHelper.BroadcastAsync(
                    _signalR,
                    "ExitMessageHub",
                    "ExitMessageMethod",
                    "Not Allowed",
                    _log,
                    ct);
                return Result<UpdateSecondWeightResult>.Fail("VRAC_NET_WEIGHT_OUT_OF_RANGE");
            }
        }




        try
        {
            // 1️⃣ Update second weight (DeuxiemePoid, PabExitAt, Step=5, Weight_Charged, etc.)
            //    This persists the weigh values — do NOT repeat in the provisional path below.
            var spResult = await conn.QueryFirstOrDefaultAsync<RowsDto>(
                new CommandDefinition(
                    """
                    DECLARE @Now DATETIME =
                        CONVERT(DATETIME, SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time');

                    UPDATE dbo.Ecare_Order_Legend
                    SET
                        DeuxiemePoid = @DeuxiemePoid,
                        PabSortieDeviceId = @DeviceId,
                        Weight_Charged = @DeuxiemePoid - PremierePoid,
                        NumberSacs_Charged = ISNULL(SacNumber, 0),
                        PabExitAt = @Now,
                        Step = 5,
                        ElapsedTimeInF_Exit = DATEDIFF(MINUTE, FinishedChargingAt, @Now),
                        TotalTimeInCercuit =
                            ISNULL(ElapsedTimeParking, 0) +
                            ISNULL(ElapsedInPab_Charging, 0) +
                            ISNULL(ElapsedCharging, 0) +
                            DATEDIFF(MINUTE, FinishedChargingAt, @Now)
                    WHERE Id = @LegendId
                      AND Step < 5
                      AND ISNULL(AnnulationCommercial, 0) <> 1
                      AND ISNULL(Status, '') <> 'Canceled';

                    DECLARE @RowsAffected INT = @@ROWCOUNT;

                    IF @RowsAffected = 1
                    BEGIN
                        UPDATE L
                        SET RealtimeCapacity =
                            CASE
                                WHEN ISNULL(L.RealtimeCapacity, 0) < ISNULL(L.Capacity, 0)
                                    THEN ISNULL(L.RealtimeCapacity, 0) + 1
                                ELSE ISNULL(L.Capacity, 0)
                            END
                        FROM dbo.Ecare_Ligne L
                        INNER JOIN dbo.Ecare_Order_Legend O ON O.Ligne = L.Nom
                        WHERE O.Id = @LegendId
                          AND O.Ligne IS NOT NULL
                          AND LTRIM(RTRIM(O.Ligne)) <> '';
                    END

                    IF @RowsAffected = 1 AND @OrderId IS NOT NULL
                    BEGIN
                        UPDATE dbo.Orders
                        SET Statut = 'Termine'
                        WHERE Id = @OrderId;
                    END

                    SELECT @RowsAffected AS RowsAffected, @OrderId AS UpdatedOrderId;
                    """,
                    new
                    {
                        LegendId = order.Id,
                        request.DeuxiemePoid,
                        order.OrderId,
                        request.DeviceId
                    },
                    cancellationToken: ct));

            if (spResult is null || spResult.RowsAffected == 0)
                return Result<UpdateSecondWeightResult>.Fail("NO_ROW_UPDATED");

            // 2️⃣ Build the SAP request identifier
            var legendId = order.Id;

            // 3️⃣ Call SAP — either the offline sync endpoint or the normal shipmentNotification.
            //    ANY failure → provisional path: truck always exits.
            BlJson? shipment = null;
            bool sapSucceeded = false;
            // Snapshot loaded eagerly on the sync-success path so the provisional block can
            // reuse it without issuing a second SELECT.
            OfflineLegendSnapshot? cachedSnap = null;

            var target = OfflineSecondWeightRules.ResolveSapTarget(order.IsOffline);

            if (target == "sync")
            {
                // Legend is already offline — try to sync via Task-2 endpoint.
                try
                {
                    var syncUrl = $"{SapBaseUrl}/api/SapOffline/sync/{legendId}";
                    var syncResp = await _http.PostAsJsonAsync(syncUrl, (object?)null, ct);

                    if (syncResp.IsSuccessStatusCode)
                    {
                        var syncResult = await syncResp.Content.ReadFromJsonAsync<SapOfflineSyncResponse>(
                            new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);

                        if (syncResult?.Status == "Synchronisee" &&
                            !string.IsNullOrWhiteSpace(syncResult.BonDeLivraison))
                        {
                            // Sync succeeded — persist and continue normal flow.
                            await PersistShipmentAndReleaseCapacityAsync(
                                conn, legendId, syncResult.BonDeLivraison, ct);

                            // Load the snapshot now so the provisional SELECT below is skipped,
                            // and build the full BL payload (client/transport/pesage/produits).
                            // Then stamp the real BonDeLivraison from SAP so the printer renders
                            // a complete, normal BL — not a provisional one.
                            cachedSnap = await conn.QuerySingleOrDefaultAsync<OfflineLegendSnapshot>(
                                new CommandDefinition(
                                    """
                                    SELECT TOP (1)
                                        Site,
                                        ClientName,
                                        Chantier,
                                        BonDeCommande,
                                        CodeSapClient,
                                        Matricule,
                                        ChauffeurName,
                                        TransporteurName,
                                        PermisDeConduite,
                                        Plombs,
                                        TypeProduit,
                                        CodeSapProduit1,
                                        CodeSapProduit2,
                                        Produit1,
                                        Produit2,
                                        Quantite1,
                                        Quantite2,
                                        SacNumber,
                                        PremierePoid,
                                        DeuxiemePoid,
                                        PabEntryAt,
                                        PabExitAt,
                                        NumberSacs_Charged,
                                        Weight_Charged
                                    FROM dbo.Ecare_Order_Legend
                                    WHERE Id = @LegendId
                                    """,
                                    new { LegendId = legendId },
                                    cancellationToken: ct));

                            shipment = BuildProvisionalBlPayload(legendId, cachedSnap);
                            // Override the null BonDeLivraison with the real SAP number so the
                            // printer knows this is a fully-synced BL, not a provisional one.
                            shipment.BonDeLivraison = syncResult.BonDeLivraison;
                            sapSucceeded = true;
                        }
                        else
                        {
                            // Sync returned EnAttente or Erreur — stay provisional.
                            var syncStatus = syncResult?.Status ?? "EnAttente";
                            var syncError = syncResult?.Error;
                            bool isBusiness = syncStatus == "Erreur";
                            var (_, offlineSt, offlineErr) = OfflineSecondWeightRules.OnSapFailure(
                                wasOffline: true,
                                isConnectivity: !isBusiness,
                                syncError ?? syncStatus);

                            await MarkOfflineProvisionalAsync(
                                conn, legendId, offlineSt, offlineErr, ct);
                        }
                    }
                    else
                    {
                        // Non-2xx from sync endpoint → business error.
                        var errMsg = $"SapOffline/sync returned {(int)syncResp.StatusCode}";
                        var (_, offlineSt, offlineErr) = OfflineSecondWeightRules.OnSapFailure(
                            wasOffline: true, isConnectivity: false, errMsg);
                        await MarkOfflineProvisionalAsync(conn, legendId, offlineSt, offlineErr, ct);
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
                {
                    _log.LogWarning(ex, "Connectivity error calling SapOffline/sync for LegendId={Id}", legendId);
                    var (_, offlineSt, offlineErr) = OfflineSecondWeightRules.OnSapFailure(
                        wasOffline: true, isConnectivity: true, ex.Message);
                    await MarkOfflineProvisionalAsync(conn, legendId, offlineSt, offlineErr, ct);
                }
            }
            else
            {
                // Normal path: call shipmentNotification with connectivity guard.
                try
                {
                    var sapRequest = new ShipmentNotificationRequest { Id = legendId };
                    var response = await _http.PostAsJsonAsync(
                        $"{SapBaseUrl}/api/SapShipment/shipmentNotification",
                        sapRequest,
                        ct);

                    if (response.IsSuccessStatusCode)
                    {
                        var parsed = await response.Content.ReadFromJsonAsync<BlJson>(ct);

                        if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.BonDeLivraison))
                        {
                            await PersistShipmentAndReleaseCapacityAsync(
                                conn, legendId, parsed.BonDeLivraison, ct);

                            shipment = new BlJson
                            {
                                Site = parsed.Site,
                                BonDeLivraison = parsed.BonDeLivraison,
                                IdMyCimar = legendId,
                                Client = new ClientJson
                                {
                                    CodeSap = parsed.Client?.CodeSap,
                                    Name = parsed.Client?.Name,
                                    Chantier = parsed.Client?.Chantier,
                                    BonDeCommande = parsed.Client?.BonDeCommande
                                },
                                Transport = new TransportJson
                                {
                                    Transporteur = parsed.Transport?.Transporteur,
                                    Matricule = parsed.Transport?.Matricule,
                                    Chauffeur = parsed.Transport?.Chauffeur,
                                    Scelles = parsed.Transport?.Scelles,
                                    Cin = parsed.Transport?.Cin,
                                },
                                Pesage = new PesageJson
                                {
                                    PoidsVide = parsed.Pesage?.PoidsVide,
                                    PoidsBrut = parsed.Pesage?.PoidsBrut,
                                    PabEntryAt = parsed.Pesage?.PabEntryAt,
                                    PabExitAt = parsed.Pesage?.PabExitAt
                                },
                                Produits = parsed.Produits?.Select(p => new ProductJson
                                {
                                    Code = p.Code,
                                    Libelle = p.Libelle,
                                    Quantite = p.Quantite,
                                    Sacs = p.Sacs
                                }).ToList()
                            };
                            sapSucceeded = true;
                        }
                        else
                        {
                            // 2xx but empty BL — treat as business error.
                            _log.LogWarning("SAP shipmentNotification returned empty BL for LegendId={Id}", legendId);
                            var (_, offlineSt, offlineErr) = OfflineSecondWeightRules.OnSapFailure(
                                wasOffline: false, isConnectivity: false, "SAP_EMPTY_RESPONSE");
                            await MarkOfflineProvisionalAsync(conn, legendId, offlineSt, offlineErr, ct);
                        }
                    }
                    else
                    {
                        _log.LogError(
                            "SAP shipment API failed for LegendId={Id}, Status={Status}",
                            legendId, response.StatusCode);
                        var errMsg = $"HTTP {(int)response.StatusCode}";
                        var (_, offlineSt, offlineErr) = OfflineSecondWeightRules.OnSapFailure(
                            wasOffline: false, isConnectivity: false, errMsg);
                        await MarkOfflineProvisionalAsync(conn, legendId, offlineSt, offlineErr, ct);
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
                {
                    _log.LogWarning(ex, "Connectivity error calling SapShipment for LegendId={Id}", legendId);
                    var (_, offlineSt, offlineErr) = OfflineSecondWeightRules.OnSapFailure(
                        wasOffline: false, isConnectivity: true, ex.Message);
                    await MarkOfflineProvisionalAsync(conn, legendId, offlineSt, offlineErr, ct);
                }
            }

            // 4️⃣ Build and broadcast the print payload.
            //    If SAP succeeded, shipment is already populated above.
            //    If provisional (sapSucceeded=false), build payload from DB.
            BlJson printPayload;

            if (sapSucceeded && shipment is not null)
            {
                printPayload = shipment;
            }
            else
            {
                // Provisional path: build BL payload locally (BonDeLivraison=null, IdMyCimar=legendId).
                // Reuse cachedSnap if it was already loaded on the sync-success path above;
                // otherwise fetch it now (normal offline failures and normal-path failures).
                var snap = cachedSnap ?? await conn.QuerySingleOrDefaultAsync<OfflineLegendSnapshot>(
                    new CommandDefinition(
                        """
                        SELECT TOP (1)
                            Site,
                            ClientName,
                            Chantier,
                            BonDeCommande,
                            CodeSapClient,
                            Matricule,
                            ChauffeurName,
                            TransporteurName,
                            PermisDeConduite,
                            Plombs,
                            TypeProduit,
                            CodeSapProduit1,
                            CodeSapProduit2,
                            Produit1,
                            Produit2,
                            Quantite1,
                            Quantite2,
                            SacNumber,
                            PremierePoid,
                            DeuxiemePoid,
                            PabEntryAt,
                            PabExitAt,
                            NumberSacs_Charged,
                            Weight_Charged
                        FROM dbo.Ecare_Order_Legend
                        WHERE Id = @LegendId
                        """,
                        new { LegendId = legendId },
                        cancellationToken: ct));

                printPayload = BuildProvisionalBlPayload(legendId, snap);
            }

            // Broadcast to printer hub.
            await SignalRHelper.BroadcastAsync(
                _signalR,
                _opt.Hub,
                _opt.Method,
                printPayload,
                _log,
                ct);

            // 5️⃣ Broadcast "Allowed" — truck exits regardless of SAP outcome.
            await SignalRHelper.BroadcastAsync(
                _signalR,
                "ExitMessageHub",
                "ExitMessageMethod",
                "Allowed",
                _log,
                ct);

            // 6️⃣ Return success
            return Result<UpdateSecondWeightResult>.Ok(new UpdateSecondWeightResult
            {
                Success = true,
                BonDeLivraison = new BonDeLivraisonDto { Id = legendId }
            });
        }
        catch (Exception ex)
        {
            _log.LogError(
                ex,
                "Error updating second weight. LegendId={LegendId}, RFID={Rfid}, Matricule={Matricule}, DeuxiemePoid={DeuxiemePoid}",
                request.LegendId,
                request.RfidCard,
                request.Matricule,
                request.DeuxiemePoid);

            return Result<UpdateSecondWeightResult>.Fail($"UNEXPECTED_ERROR: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private sealed class RowsDto
    {
        public int RowsAffected { get; set; }
        public int? UpdatedOrderId { get; set; }
    }

    /// <summary>
    /// Persists the real BonDeLivraison from SAP and marks the legend Completed+IsSynced.
    /// Only called on SAP success.
    /// </summary>
    private static async Task PersistShipmentAndReleaseCapacityAsync(
        SqlConnection conn,
        int legendId,
        string bonDeLivraison,
        CancellationToken ct)
    {
        const string sql = """
            UPDATE dbo.Ecare_Order_Legend
            SET
                BonDeLivraison = @BonDeLivraison,
                IsSynced = 1,
                Status = CASE
                    WHEN ISNULL(AnnulationCommercial, 0) = 1 THEN 'Canceled'
                    ELSE 'Completed'
                END
            WHERE Id = @LegendId
              AND ISNULL(BonDeLivraison, '') = '';
            """;

        await conn.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    LegendId = legendId,
                    BonDeLivraison = bonDeLivraison
                },
                cancellationToken: ct));
    }

    /// <summary>
    /// Marks the legend as offline-provisional (no BonDeLivraison, IsSynced remains 0).
    /// Status='Completed' and Step=5 were already set by the main UPDATE above.
    /// Sets IsOffline=1, OfflineStatus, OfflineSyncError, OfflineCreatedAt=COALESCE(existing,@now).
    /// Note: capacity release (RealtimeCapacity increment) is performed by the initial weight UPDATE
    /// at the top of Handle — this method does NOT touch RealtimeCapacity.
    /// </summary>
    private static async Task MarkOfflineProvisionalAsync(
        SqlConnection conn,
        int legendId,
        string offlineStatus,
        string? offlineSyncError,
        CancellationToken ct)
    {
        const string sql = """
            DECLARE @Now DATETIME =
                CONVERT(DATETIME, SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time');

            UPDATE dbo.Ecare_Order_Legend
            SET
                IsOffline        = 1,
                OfflineStatus    = @OfflineStatus,
                OfflineSyncError = @OfflineSyncError,
                OfflineCreatedAt = COALESCE(OfflineCreatedAt, @Now),
                Status           = CASE
                    WHEN ISNULL(AnnulationCommercial, 0) = 1 THEN 'Canceled'
                    ELSE 'Completed'
                END,
                Step = 5
            WHERE Id = @LegendId;
            """;

        await conn.ExecuteAsync(
            new CommandDefinition(
                sql,
                new { LegendId = legendId, OfflineStatus = offlineStatus, OfflineSyncError = offlineSyncError },
                cancellationToken: ct));
    }

    /// <summary>
    /// Builds the provisional BL JSON payload from a local legend snapshot.
    /// BonDeLivraison is null; IdMyCimar carries the internal legend id.
    /// Uses the same weight formatting as the normal SAP-supplied payload.
    /// Public so ReprintBonLivraisonHandler can reuse without duplication.
    /// </summary>
    public static BlJson BuildProvisionalBlPayload(int legendId, OfflineLegendSnapshot? snap)
    {
        if (snap is null)
        {
            return new BlJson
            {
                BonDeLivraison = null,
                IdMyCimar = legendId
            };
        }

        // Weight formatting: "X T Y kg" matching the SAP-built payload convention.
        static string FormatWeight(int? kg)
        {
            if (kg is null or 0) return "0 T 0 kg";
            var tonnes = kg.Value / 1000;
            var remainder = kg.Value % 1000;
            return $"{tonnes} T {remainder} kg";
        }

        var produits = new List<ProductJson>();

        if (!string.IsNullOrWhiteSpace(snap.Produit1))
        {
            produits.Add(new ProductJson
            {
                Code = snap.CodeSapProduit1?.PadLeft(18, '0'),
                Libelle = snap.Produit1,
                Quantite = snap.Quantite1?.ToString("0.###"),
                Sacs = snap.SacNumber
            });
        }

        if (!string.IsNullOrWhiteSpace(snap.Produit2))
        {
            produits.Add(new ProductJson
            {
                Code = snap.CodeSapProduit2?.PadLeft(18, '0'),
                Libelle = snap.Produit2,
                Quantite = snap.Quantite2?.ToString("0.###"),
                Sacs = null
            });
        }

        // Parse plombs (scellés) from comma-separated string.
        var scelles = string.IsNullOrWhiteSpace(snap.Plombs)
            ? new List<string>()
            : snap.Plombs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .ToList();

        return new BlJson
        {
            Site = snap.Site,
            BonDeLivraison = null,
            IdMyCimar = legendId,
            Client = new ClientJson
            {
                CodeSap = snap.CodeSapClient?.PadLeft(10, '0'),
                Name = snap.ClientName,
                Chantier = snap.Chantier,
                BonDeCommande = snap.BonDeCommande
            },
            Transport = new TransportJson
            {
                Transporteur = snap.TransporteurName,
                Matricule = snap.Matricule,
                Chauffeur = snap.ChauffeurName,
                Cin = snap.PermisDeConduite,
                Scelles = scelles
            },
            Pesage = new PesageJson
            {
                PoidsVide = FormatWeight(snap.PremierePoid),
                PoidsBrut = FormatWeight(snap.DeuxiemePoid),
                PabEntryAt = snap.PabEntryAt,
                PabExitAt = snap.PabExitAt
            },
            Produits = produits.Count > 0 ? produits : null
        };
    }
}

#endregion
