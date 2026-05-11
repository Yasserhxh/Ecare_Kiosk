using Dapper;
using Ecare.Application.Queries.ParkingScanOrderLegend;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Net.Http.Json;
using static Ecare.Application.Queries.ParkingScanOrderLegend.ParkingScanModels;

public sealed class ParkingScanHandler
    : IRequestHandler<ParkingScanQuery, Result<ScanResultVm>>
{
    private readonly IConfiguration _cfg;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ParkingScanHandler> _log;

    public ParkingScanHandler(
        IConfiguration cfg,
        IHttpClientFactory httpFactory,
        ILogger<ParkingScanHandler> log)
    {
        _cfg = cfg;
        _httpFactory = httpFactory;
        _log = log;
    }

    public async Task<Result<ScanResultVm>> Handle(ParkingScanQuery request, CancellationToken ct)
    {
        using var conn = new SqlConnection(_cfg.GetConnectionString("SqlServer"));
        var http = _httpFactory.CreateClient();
        try
        {
            var orderFound = await GetSingleActiveOrderByRfidAsync(conn, request.Slv);

            if (orderFound != null)
            {
                // Build a ScanResultVm containing ONE client artificially
                var vmo = new ScanResultVm
                {
                    Slv = request.Slv,
                    Clients =
                    {
                        new ClientResult
                        {
                            ClientName = orderFound.ClientName,
                            Matricule = orderFound.Matricule,
                            ChauffeurName = "",         // optional (empty if not needed)
                            CodeSapClient = orderFound.CodeSapClient,
                            Order = orderFound
                        }
                    }
                };

                // RETURN like CASE 2 — ORDER_FOUND
                return Result<ScanResultVm>.Ok(vmo);
            }




            // STEP 2 — GET CLIENTS
            var equips = (await conn.QueryAsync<dynamic>(
                "SELECT * FROM Ecare_ClientEquipements WHERE CarteSLV = @slv AND ( IsTransporteur=1 OR IsClient=1 )",
                new { slv = request.Slv }
            )).ToList();

            var vm = new ScanResultVm { Slv = request.Slv };

            if (!equips.Any())
                return Result<ScanResultVm>.Ok(vm); // Case 1

            // PROCESS EACH CLIENT
            foreach (var e in equips)
            {
                var clientNode = new ClientResult
                {
                    ClientName = e.ClientName,
                    Matricule = e.Matricule,
                    ChauffeurName = e.ChauffeurName,
                    CodeSapClient = e.CodeClientSAP
                };

                // STEP 2 — CHECK LAST ORDER
                var order = await conn.QueryFirstOrDefaultAsync<OrderLegendVm>(
                    GetScopedActiveOrderSql,
                    new
                    {
                        RFIDCard = request.Slv,
                        Matricule = (string?)e.Matricule,
                        CodeSapClient = (string?)e.CodeClientSAP,
                    }
                );

                if (order != null)
                {
                    clientNode.Order = order; // Case 2
                }
                else
                {
                    // STEP 3 — NO ORDER → FETCH CHANTIERS FROM SAP
                    if (!string.IsNullOrWhiteSpace(e.CodeClientSAP))
                    {
                        var sapReq = new SapRequest
                        {
                            CodeClient = e.CodeClientSAP
                        };

                        var res = await http.PostAsJsonAsync(
                            "https://app-emea-we-dssprod-dss-001.azurewebsites.net/api/SapOrders/partners/chantiers",
                            sapReq
                        );

                        if (res.IsSuccessStatusCode)
                        {
                            var sap = await res.Content.ReadFromJsonAsync<SapResponse>();

                            foreach (var ch in sap?.chantiers ?? new())
                            {
                                clientNode.Chantiers.Add(new ChantierVm
                                {
                                    CodeSapChantier = ch.kunN2,
                                    NomChantier = ch.namE1
                                });
                            }
                        }
                    }
                }

                vm.Clients.Add(clientNode);
            }

            return Result<ScanResultVm>.Ok(vm);
        }
        catch(Exception ex)
        {
            return Result<ScanResultVm>.Fail(ex.ToString());

        }

    }

    private const string SelectOrderColumns = @"
SELECT
    L.Id AS LegendId,
    L.CodeSapCommande,
    L.BonDeCommande,
    L.Matricule,
    L.Produit1,
    L.Produit2,
    L.Quantite1,
    L.Quantite2,
    L.PremierePoid,
    L.DeuxiemePoid,
    L.Step,
    L.CodeSapClient,
    L.StartChargingAt,
    L.RFIDCard,
    C1.ImageUrl AS Produit1Image,
    C2.ImageUrl AS Produit2Image,
    L.ClientName,
    L.ChauffeurName
FROM dbo.Ecare_Order_Legend L
LEFT JOIN dbo.EcareCiments C1 ON C1.Name = L.Produit1
LEFT JOIN dbo.EcareCiments C2 ON C2.Name = L.Produit2
";

    private const string GetSingleActiveOrderByRfidSql = SelectOrderColumns + @"
WHERE L.RFIDCard = @RFIDCard
  AND ISNULL(L.Step, 0) < 5
ORDER BY L.Id DESC;
";

    private const string GetScopedActiveOrderSql = SelectOrderColumns + @"
WHERE L.RFIDCard = @RFIDCard
  AND ISNULL(L.Step, 0) < 5
  AND
  (
      (NULLIF(@Matricule, '') IS NOT NULL AND L.Matricule = @Matricule)
      OR
      (NULLIF(@CodeSapClient, '') IS NOT NULL AND L.CodeSapClient = @CodeSapClient)
  )
ORDER BY
    CASE WHEN NULLIF(@Matricule, '') IS NOT NULL AND L.Matricule = @Matricule THEN 0 ELSE 1 END,
    L.Id DESC;
";

    private static async Task<OrderLegendVm?> GetSingleActiveOrderByRfidAsync(SqlConnection conn, string slv)
    {
        var rows = (await conn.QueryAsync<OrderLegendVm>(
            GetSingleActiveOrderByRfidSql,
            new { RFIDCard = slv }))
            .Take(2)
            .ToList();

        return rows.Count == 1 ? rows[0] : null;
    }
}
