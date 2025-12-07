using Dapper;
using Ecare.Application.Queries.ParkingScanOrderLegend;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
            // STEP 1 — GET CLIENTS
            var equips = (await conn.QueryAsync<dynamic>(
                "SELECT * FROM Ecare_ClientEquipements WHERE CarteSLV = @slv",
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
                    "Parking_Scan_OrderLegend",
                    new
                    {
                        RFIDCard = request.Slv,

                    },
                    commandType: System.Data.CommandType.StoredProcedure
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
}
