using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Text;

namespace Ecare.Application.Commands.CreateLegacyOrderLegend
{
    public sealed class CreateLegacyOrderLegendHandler
    : IRequestHandler<CreateLegacyOrderLegendCommand, Result<int>>
    {
        private readonly string _connString;

        public CreateLegacyOrderLegendHandler(IConfiguration cfg)
        {
            _connString = cfg.GetConnectionString("SqlServer")
                ?? throw new InvalidOperationException("Missing SqlServer connection string");
        }

        public async Task<Result<int>> Handle(
            CreateLegacyOrderLegendCommand request,
            CancellationToken ct)
        {
            try
            {
                await using var conn = new SqlConnection(_connString);
                await conn.OpenAsync(ct);

                var p = new DynamicParameters();

                p.Add("@BonDeCommande", request.BonDeCommande);
                p.Add("@ClientName", request.ClientName);
                p.Add("@Chantier", request.Chantier);
                p.Add("@Matricule", request.Matricule);
                p.Add("@RFIDCard", request.RFIDCard);
                p.Add("@TypeCamion", request.TypeCamion);
                p.Add("@NombrePlombs", request.NombrePlombs);

                p.Add("@Produit1", request.Produit1);
                p.Add("@Quantite1", request.Quantite1);
                p.Add("@Produit2", request.Produit2);
                p.Add("@Quantite2", request.Quantite2);

                p.Add("@TypeProduit", request.TypeProduit);
                p.Add("@ChequeImg", request.ChequeImg);
                p.Add("@AddedToQueueAt", request.AddedToQueueAt);

                int legendId = await conn.ExecuteScalarAsync<int>(
                    "sp_InitLegacyOrder",
                    p,
                    commandType: CommandType.StoredProcedure
                );

                if (request.Event == "CLIENTS_WITH_CHANTIERS")
                {
                    // Shared values
                    var url = "https://app-emea-we-dssdev-mycimar-api-001.azurewebsites.net/api/SapOrders/createOrder";

                    var http = new HttpClient();
                    http.Timeout = TimeSpan.FromSeconds(40);

                    HttpContent content;

                    if (!string.IsNullOrWhiteSpace(request.Produit2))
                    {
                        // ==========================
                        //     TWO ITEMS PAYLOAD
                        // ==========================
                        var payload = new
                        {
                            codeClient = "0001254277",
                            date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                            purchNoC = request.BonDeCommande,
                            salesOrg = "MA18",

                            material = "000010",
                            material2 = "000010",

                            plant = "M108",

                            quantity = request.Quantite1,
                            quantity2 = request.Quantite2,

                            itemNumber = "000010",
                            itemNumber2 = "000020",

                            soldTo = "0001254277",
                            shipTo = "0021714661",

                            reqDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            reqQty = request.Quantite1,
                            reqQty2 = request.Quantite2,

                            behaveWhenError = "",
                            incoterms1 = "EXW",
                            incoterms2 = "DEPART",
                            intNumberAssignment = "",
                            testRun = true
                        };

                        content = new StringContent(
                            System.Text.Json.JsonSerializer.Serialize(payload),
                            Encoding.UTF8,
                            "application/json"
                        );
                    }
                    else
                    {
                        // ==========================
                        //     ONE ITEM PAYLOAD
                        // ==========================
                        var payload = new
                        {
                            codeClient = "0001254277",
                            date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                            purchNoC = $"AUTO ORDER {request.Matricule} one item",
                            salesOrg = "MA18",

                            material = "000010",
                            plant = "M108",

                            quantity = request.Quantite1,

                            itemNumber = "000010",

                            soldTo = "0001254277",
                            shipTo = "0021714661",

                            reqDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            reqQty = request.Quantite1,

                            behaveWhenError = "",
                            incoterms1 = "EXW",
                            incoterms2 = "DEPART",
                            intNumberAssignment = "",
                            testRun = true
                        };

                        content = new StringContent(
                            System.Text.Json.JsonSerializer.Serialize(payload),
                            Encoding.UTF8,
                            "application/json"
                        );
                    }

                    // ==========================
                    // SEND REQUEST
                    // ==========================
                    var response = await http.PostAsync(url, content);

                    var apiResponse = await response.Content.ReadAsStringAsync();

                    // You may log this or return inside your handler
                    Console.WriteLine("SAP API RESPONSE:");
                    Console.WriteLine(apiResponse);
                }


                return Result<int>.Ok(legendId);
            }
            catch (Exception ex)
            {
                return Result<int>.Fail("LEGEND_INIT_ERROR: " + ex.Message);
            }
        }
    }

}
