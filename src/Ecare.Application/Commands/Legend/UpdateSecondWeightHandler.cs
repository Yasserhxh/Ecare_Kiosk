using Dapper;
using Ecare.Application.Dtos;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;
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
    public string? TypeProduit { get; set; }
    public int PremierePoid { get; set; }
    public int Quantite1 { get; set; }
    public int Quantite2 { get; set; }
    public int PTAC { get; set; }
    public int Step { get; set; }
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
            SELECT
                Id,
                TypeProduit,
                Matricule,
                PremierePoid,
                Quantite1,
                Quantite2,
                PTAC,
                Step
            FROM dbo.Ecare_Order_Legend
            WHERE RFIDCard = @RFIDCard
              AND Matricule = @Matricule
              AND Step>2
            """,
            new { RFIDCard = request.RfidCard, Matricule = request.Matricule }
        );

        if (order is null)
            return Result<UpdateSecondWeightResult>.Fail("ORDER_NOT_FOUND");

        if (order.TypeProduit is "SAC" or "PAL")
        {
            // Net weight
            var net = request.DeuxiemePoid - order.PremierePoid;

            // Expected weight in grams (or kg*1000 depending on your units)
            var expected = (order.Quantite1 * 1000m) + (order.Quantite2 * 1000m);

            // 1% tolerance
            var tolerance = expected * 0.02m;

            var minAllowed = expected - tolerance;
            var maxAllowed = expected + tolerance;

            // Compare using decimal to avoid rounding surprises
            if (net < minAllowed || net > maxAllowed)
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
            var allowedMax = order.PTAC * 1.11m; // +10% tolerance
            if(request.DeuxiemePoid > allowedMax)
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




        try
        {
                // 1️⃣ Update second weight
                var spResult = await conn.QueryFirstOrDefaultAsync<RowsDto>(
                    "sp_UpdateSecondWeight",
                    new
                    {
                        RfidCard = request.RfidCard,
                        Matricule = request.Matricule,
                        DeuxiemePoid = request.DeuxiemePoid
                    },
                    commandType: CommandType.StoredProcedure);

                //if (spResult is null || spResult.RowsAffected == 0)
                //    return Result<UpdateSecondWeightResult>.Fail("NO_ROW_UPDATED");

                // 2️⃣ Get latest BL Id
                var blData = await conn.QuerySingleOrDefaultAsync<BonDeLivraisonDto>(
                    """
                SELECT TOP (1)
                    Id
                FROM dbo.Ecare_Order_Legend
                WHERE RfidCard = @RfidCard
                  AND Matricule = @Matricule
                ORDER BY PabExitAt DESC
                """,
                    new { request.RfidCard, request.Matricule });

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

                //if (!response.IsSuccessStatusCode)
                //{
                //    _log.LogError(
                //        "SAP shipment API failed for Id={Id}, Status={Status}",
                //        blData.Id,
                //        response.StatusCode);

                //    return Result<UpdateSecondWeightResult>.Fail("SAP_API_ERROR");
                //}

                var shipment =
                    await response.Content.ReadFromJsonAsync<BlJson>(ct);

                //if (shipment is null)
                //    return Result<UpdateSecondWeightResult>.Fail("SAP_EMPTY_RESPONSE");


                var signalRPayload = new BlJson
                {
                    Site = shipment.Site,
                    BonDeLivraison = shipment.BonDeLivraison,
                    Client = new ClientJson
                    {
                        CodeSap = shipment.Client.CodeSap,
                        Name = shipment.Client.Name,
                        Chantier = shipment.Client.Chantier,
                        BonDeCommande = shipment.Client.BonDeCommande
                    },
                    Transport = new TransportJson
                    {
                        Transporteur = shipment.Transport.Transporteur,
                        Matricule = shipment.Transport.Matricule,
                        Chauffeur = shipment.Transport.Chauffeur,
                        Scelles = shipment.Transport.Scelles,
                        Cin = shipment.Transport.Cin,
                    },
                    Pesage = new PesageJson
                    {
                        PoidsVide = shipment.Pesage.PoidsVide,
                        PoidsBrut = shipment.Pesage.PoidsBrut,
                        PabEntryAt = shipment.Pesage.PabEntryAt,
                        PabExitAt = shipment.Pesage.PabExitAt
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
                    "Error updating second weight for RFID={Rfid}",
                    request.RfidCard);

                return Result<UpdateSecondWeightResult>.Fail("UNEXPECTED_ERROR");
            }
    }

    private sealed class RowsDto
    {
        public int RowsAffected { get; set; }
        public int? UpdatedOrderId { get; set; }
    }


   



}

#endregion
