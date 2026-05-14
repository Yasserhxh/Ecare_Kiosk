using Dapper;
using Ecare.Application.Services;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;

namespace Ecare.Application.Commands.Legend;

public sealed record ReprintBonLivraisonCommand(int Id) : IRequest<Result<BlJson>>;

public sealed class ReprintBonLivraisonHandler
    : IRequestHandler<ReprintBonLivraisonCommand, Result<BlJson>>
{
    private readonly ILogger<ReprintBonLivraisonHandler> _log;
    private readonly ServiceManager _signalR;
    private readonly PrintOutboundOptions _opt;
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    public ReprintBonLivraisonHandler(
        ILogger<ReprintBonLivraisonHandler> log,
        ServiceManager signalR,
        IOptions<PrintOutboundOptions> opt,
        IHttpClientFactory httpFactory,
        IConfiguration cfg)
    {
        _log = log;
        _signalR = signalR;
        _opt = opt.Value;
        _http = httpFactory.CreateClient("SapShipment");
        _cfg = cfg;
    }

    public async Task<Result<BlJson>> Handle(ReprintBonLivraisonCommand request, CancellationToken ct)
    {
        if (request.Id <= 0)
            return Result<BlJson>.Fail("INVALID_LEGEND_ID");

        try
        {
            var connStr = _cfg.GetConnectionString("SqlServer");
            if (string.IsNullOrWhiteSpace(connStr))
                return Result<BlJson>.Fail("MISSING_SQL_CONNECTION");

            await using var conn = new SqlConnection(connStr);

            var legend = await conn.QuerySingleOrDefaultAsync<ReprintLegendRow>(
                new CommandDefinition(
                    """
                    SELECT TOP (1)
                        Id,
                        BonDeLivraison,
                        Step,
                        PabEntryAt,
                        PremierePoid,
                        PabExitAt,
                        DeuxiemePoid,
                        Quantite1,
                        Quantite2,
                        TypeProduit,
                        PTAC,
                        TARE
                    FROM dbo.Ecare_Order_Legend
                    WHERE Id = @Id;
                    """,
                    new { request.Id },
                    cancellationToken: ct));

            if (legend is null)
                return Result<BlJson>.Fail("LEGEND_NOT_FOUND");

            if (!legend.PabExitAt.HasValue || !legend.DeuxiemePoid.HasValue || legend.DeuxiemePoid.Value <= 0)
            {
                _log.LogWarning(
                    "Blocked BL print/create for LegendId={Id}: second weight is missing. PabExitAt={PabExitAt}, DeuxiemePoid={DeuxiemePoid}",
                    request.Id,
                    legend.PabExitAt,
                    legend.DeuxiemePoid);

                return Result<BlJson>.Fail("SECOND_WEIGHT_REQUIRED_FOR_BL");
            }

            var validationError = ValidateSecondWeightForBl(legend);
            if (validationError is not null)
            {
                _log.LogWarning(
                    "Blocked BL print/create for LegendId={Id}: second weight validation failed with {Error}",
                    request.Id,
                    validationError);

                return Result<BlJson>.Fail(validationError);
            }

            var shipment = await TryGetExistingShipmentAsync(request.Id, ct);

            if (shipment is null && string.IsNullOrWhiteSpace(legend.BonDeLivraison))
            {
                shipment = await CreateShipmentAsync(request.Id, ct);

                if (shipment is null || string.IsNullOrWhiteSpace(shipment.BonDeLivraison))
                    return Result<BlJson>.Fail("SAP_EMPTY_RESPONSE");

                await PersistCreatedShipmentAndReleaseCapacityAsync(
                    conn,
                    request.Id,
                    shipment.BonDeLivraison,
                    ct);
            }

            if (shipment is null)
            {
                _log.LogWarning(
                    "Cannot reprint BL because no existing SAP livraison was found for LegendId={Id}. Status={Status}",
                    request.Id,
                    "NotFound");

                return Result<BlJson>.Fail("SAP_LIVRAISON_NOT_FOUND");
            }

            await SignalRHelper.BroadcastAsync(
                _signalR,
                _opt.Hub,
                _opt.Method,
                shipment,
                _log,
                ct);

            return Result<BlJson>.Ok(shipment);
        }
        catch (Exception ex)
        {
            if (ex.Message == "SAP_API_ERROR")
                return Result<BlJson>.Fail("SAP_API_ERROR");

            _log.LogError(ex, "Error reprinting BL for LegendId={Id}", request.Id);
            return Result<BlJson>.Fail("UNEXPECTED_ERROR");
        }
    }

    private async Task<BlJson?> TryGetExistingShipmentAsync(int legendId, CancellationToken ct)
    {
        var response = await _http.GetAsync(
            $"https://app-emea-we-dssprod-dss-001.azurewebsites.net/api/SapShipment/livraison/{legendId}",
            ct);

        if (response.StatusCode == HttpStatusCode.Accepted ||
            response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _log.LogError(
                "SAP livraison reprint lookup failed for LegendId={Id}, Status={Status}",
                legendId,
                response.StatusCode);

            throw new InvalidOperationException("SAP_API_ERROR");
        }

        return await response.Content.ReadFromJsonAsync<BlJson>(cancellationToken: ct);
    }

    private async Task<BlJson?> CreateShipmentAsync(int legendId, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(
            "http://app-emea-we-dssprod-dss-001.azurewebsites.net/api/SapShipment/shipmentNotification",
            new ShipmentNotificationRequest { Id = legendId },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            _log.LogError(
                "SAP shipment creation failed during BL reprint for LegendId={Id}, Status={Status}",
                legendId,
                response.StatusCode);

            throw new InvalidOperationException("SAP_API_ERROR");
        }

        return await response.Content.ReadFromJsonAsync<BlJson>(cancellationToken: ct);
    }

    private static async Task PersistCreatedShipmentAndReleaseCapacityAsync(
        SqlConnection conn,
        int legendId,
        string bonDeLivraison,
        CancellationToken ct)
    {
        const string sql = """
            DECLARE @Updated TABLE (Ligne NVARCHAR(150));

            UPDATE dbo.Ecare_Order_Legend
            SET
                BonDeLivraison = @BonDeLivraison,
                Step = 5,
                IsSynced = 1,
                Status = CASE
                    WHEN ISNULL(AnnulationCommercial, 0) = 1 THEN 'Canceled'
                    ELSE 'Completed'
                END
            OUTPUT inserted.Ligne INTO @Updated(Ligne)
            WHERE Id = @LegendId
              AND ISNULL(BonDeLivraison, '') = '';

            IF @@ROWCOUNT > 0
            BEGIN
                UPDATE L
                SET RealtimeCapacity =
                    CASE
                        WHEN ISNULL(L.RealtimeCapacity, 0) < ISNULL(L.Capacity, 0)
                            THEN ISNULL(L.RealtimeCapacity, 0) + 1
                        ELSE ISNULL(L.Capacity, 0)
                    END
                FROM dbo.Ecare_Ligne L
                WHERE L.Nom = (
                        SELECT TOP (1) U.Ligne
                        FROM @Updated U
                        WHERE U.Ligne IS NOT NULL
                    )
                  AND EXISTS (
                        SELECT 1
                        FROM dbo.Ecare_Order_Legend O
                        WHERE O.Id = @LegendId
                          AND ISNULL(O.AnnulationCommercial, 0) <> 1
                          AND (
                                O.PabEntryAt IS NOT NULL
                                OR O.PremierePoid IS NOT NULL
                          )
                    );
            END;
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

    private sealed class ReprintLegendRow
    {
        public int Id { get; init; }
        public string? BonDeLivraison { get; init; }
        public int? Step { get; init; }
        public DateTime? PabEntryAt { get; init; }
        public int? PremierePoid { get; init; }
        public DateTime? PabExitAt { get; init; }
        public int? DeuxiemePoid { get; init; }
        public decimal? Quantite1 { get; init; }
        public decimal? Quantite2 { get; init; }
        public string? TypeProduit { get; init; }
        public int? PTAC { get; init; }
        public int? TARE { get; init; }
    }

    private static string? ValidateSecondWeightForBl(ReprintLegendRow legend)
    {
        if (!legend.PremierePoid.HasValue || !legend.DeuxiemePoid.HasValue)
            return "SECOND_WEIGHT_REQUIRED_FOR_BL";

        if (legend.DeuxiemePoid.Value <= legend.PremierePoid.Value)
            return "SECOND_WEIGHT_MUST_BE_GREATER_THAN_FIRST";

        if (legend.TARE.HasValue && legend.PremierePoid.Value < legend.TARE.Value * 0.90m)
            return "FIRST_WEIGHT_BELOW_TARE";

        var quantity = (legend.Quantite1 ?? 0m) + (legend.Quantite2 ?? 0m);
        var expectedNet = quantity * 1000m;
        if (expectedNet <= 0)
            return "ORDER_QUANTITY_MISSING";

        var net = legend.DeuxiemePoid.Value - legend.PremierePoid.Value;
        var typeProduit = (legend.TypeProduit ?? string.Empty).Trim();

        if (typeProduit.Equals("SAC", StringComparison.OrdinalIgnoreCase) ||
            typeProduit.Equals("PAL", StringComparison.OrdinalIgnoreCase))
        {
            var tolerance = expectedNet * 0.02m;
            if ((decimal)net < expectedNet - tolerance || (decimal)net > expectedNet + tolerance)
                return "NET_WEIGHT_OUT_OF_RANGE";

            return null;
        }

        if (!legend.PTAC.HasValue || legend.PTAC.Value <= 0)
            return "PTAC_MISSING";

        var allowedMaxGross = legend.PTAC.Value * 1.11m;
        if (legend.DeuxiemePoid.Value > allowedMaxGross)
            return "GROSS_EXCEEDS_PTAC";

        var netTolerance = expectedNet * 0.08m;
        if ((decimal)net < expectedNet - netTolerance || (decimal)net > expectedNet + netTolerance)
            return "VRAC_NET_WEIGHT_OUT_OF_RANGE";

        var expectedGross = legend.PremierePoid.Value + expectedNet;
        var grossTolerance = expectedNet * 0.02m;
        if ((decimal)legend.DeuxiemePoid.Value < expectedGross - grossTolerance ||
            (decimal)legend.DeuxiemePoid.Value > expectedGross + grossTolerance)
            return "GROSS_WEIGHT_OUT_OF_RANGE";

        return null;
    }
}
