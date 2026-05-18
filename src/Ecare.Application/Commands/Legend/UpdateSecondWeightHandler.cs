using Dapper;
using Ecare.Application.Dtos;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
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
}

public sealed class BlJson
{
    public string? Site { get; set; }
    public string? BonDeLivraison { get; set; }
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







#endregion

#region Handler

public sealed class UpdateSecondWeightHandler
    : IRequestHandler<UpdateSecondWeightCommand, Result<UpdateSecondWeightResult>>
{
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
                PabExitAt
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
            return Result<UpdateSecondWeightResult>.Fail("ORDER_NOT_READY_FOR_EXIT");

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

            // 1% tolerance for SAC/PAL bagged products.
            var tolerance = expected * 0.01m;

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
            // 1️⃣ Update second weight
            var spResult = await conn.QueryFirstOrDefaultAsync<RowsDto>(
                new CommandDefinition(
                    """
                    DECLARE @Now DATETIME =
                        CONVERT(DATETIME, SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time');

                    UPDATE dbo.Ecare_Order_Legend
                    SET
                        DeuxiemePoid = @DeuxiemePoid,
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
                        order.OrderId
                    },
                    cancellationToken: ct));

            if (spResult is null || spResult.RowsAffected == 0)
                return Result<UpdateSecondWeightResult>.Fail("NO_ROW_UPDATED");

            // 2️⃣ Get latest BL Id
            var blData = new BonDeLivraisonDto
            {
                Id = order.Id
            };

            if (blData is null)
                return Result<UpdateSecondWeightResult>.Fail("BL_DATA_NOT_FOUND");

            // 3️⃣ Call SAP Shipment API
            var sapRequest = new ShipmentNotificationRequest
            {
                Id = blData.Id
            };

            var response = await _http.PostAsJsonAsync(
                "http://app-emea-we-dssprod-dss-001.azurewebsites.net/api/SapShipment/shipmentNotification",
                sapRequest,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogError(
                    "SAP shipment API failed for Id={Id}, Status={Status}",
                    blData.Id,
                    response.StatusCode);

                return Result<UpdateSecondWeightResult>.Fail("SAP_API_ERROR");
            }

            var shipment =
                    await response.Content.ReadFromJsonAsync<BlJson>(ct);

            if (shipment is null || string.IsNullOrWhiteSpace(shipment.BonDeLivraison))
                return Result<UpdateSecondWeightResult>.Fail("SAP_EMPTY_RESPONSE");

            await PersistShipmentAndReleaseCapacityAsync(
                conn,
                order.Id,
                shipment.BonDeLivraison,
                ct);


            var signalRPayload = new BlJson
            {
                Site = shipment.Site,
                BonDeLivraison = shipment.BonDeLivraison,
                Client = new ClientJson
                {
                    CodeSap = shipment.Client?.CodeSap,
                    Name = shipment.Client?.Name,
                    Chantier = shipment.Client?.Chantier,
                    BonDeCommande = shipment.Client?.BonDeCommande
                },
                Transport = new TransportJson
                {
                    Transporteur = shipment.Transport?.Transporteur,
                    Matricule = shipment.Transport?.Matricule,
                    Chauffeur = shipment.Transport?.Chauffeur,
                    Scelles = shipment.Transport?.Scelles,
                    Cin = shipment.Transport?.Cin,
                },
                Pesage = new PesageJson
                {
                    PoidsVide = shipment.Pesage?.PoidsVide,
                    PoidsBrut = shipment.Pesage?.PoidsBrut,
                    PabEntryAt = shipment.Pesage?.PabEntryAt,
                    PabExitAt = shipment.Pesage?.PabExitAt
                },
                Produits = shipment.Produits?.Select(p => new ProductJson
                {
                    Code = p.Code,
                    Libelle = p.Libelle,
                    Quantite = p.Quantite,
                    Sacs = p.Sacs
                }).ToList()
            };

            // 4️⃣ (Optional) Send to SignalR / printer

            await SignalRHelper.BroadcastAsync(
                _signalR,
                _opt.Hub,
                _opt.Method,
                signalRPayload,
                _log,
                ct);



            await SignalRHelper.BroadcastAsync(
                _signalR,
                "ExitMessageHub",
                "ExitMessageMethod",
                "Allowed",
                _log,
                ct);


            // 5️⃣ Return success
            return Result<UpdateSecondWeightResult>.Ok(new UpdateSecondWeightResult
            {
                Success = true,
                BonDeLivraison = blData
                // You can add Shipment = shipment if needed
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

    private sealed class RowsDto
    {
        public int RowsAffected { get; set; }
        public int? UpdatedOrderId { get; set; }
    }

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






}

#endregion
