using Azure.Core;
using Dapper;
using Ecare.Application.Commands;
using Ecare.Application.Commands.ProcessParking;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ecare.Application.Commands;

public sealed class ProcessParkingCommandHandler
    : IRequestHandler<ProcessParkingCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _cfg;
    private readonly IHttpClientFactory _httpClient;
    private readonly ILogger<ProcessParkingCommandHandler> _log;

    public ProcessParkingCommandHandler(
        IUnitOfWork uow,
        IConfiguration cfg,
        IHttpClientFactory httpClient,
        ILogger<ProcessParkingCommandHandler> log)
    {
        _uow = uow;
        _cfg = cfg;
        _httpClient = httpClient;
        _log = log;
    }

    public async Task<Result<int>> Handle(ProcessParkingCommand command, CancellationToken ct)
    {
        var r = command.Request;

        await _uow.BeginAsync(ct);

        try
        {
            // =====================================================================
            // CASE 1 — ORDER FOUND → Update Step + ParkingAt
            // =====================================================================
            if (r.Event == "ORDER_FOUND")
            {
                const string sql = @"
                 UPDATE Ecare_Order_Legend SET Step=1, ParkingAt=@Now Where RFIDCard=@Slv
            ";


                await _uow.Connection.ExecuteAsync(
                    sql,
                    new { Slv = r.Slv, Now = DateTime.Now },
                    _uow.Transaction
                );

                await _uow.CommitAsync(ct);
                return Result<int>.Ok(1);
            }

            // =====================================================================
            // CASE 2 — NO ORDER + NO CLIENT → Insert minimal order
            // =====================================================================
            if (r.Event == "NO_ORDER_NO_CLIENT")
            {
                const string sql = @"
                INSERT INTO Ecare_Order_Legend
                (Matricule, RFIDCard, TypeCamion, NombrePlombs, ParkingAt, Step)
                VALUES
                (
                    @Matricule,
                    @Slv,
                    (SELECT TOP 1 Type FROM Ecare_ClientEquipements WHERE Matricule = @Matricule),
                    (SELECT TOP 1 PlombsNumber FROM Ecare_ClientEquipements WHERE Matricule = @Matricule),
                    @Now,
                    1
                );
            ";


                await _uow.Connection.ExecuteAsync(
                    sql,
                    new
                    {
                        r.Matricule,
                        Slv = r.Slv,
                        r.TypeCamion,
                        r.NombrePlombs,
                        Now = DateTime.Now
                    },
                    _uow.Transaction
                );

                await _uow.CommitAsync(ct);
                return Result<int>.Ok(1);
            }

            // =====================================================================
            // CASE 3 — CLIENT + CHANTIER + PRODUCTS → Insert + SAP Order
            // =====================================================================
            if (r.Event == "CLIENTS_WITH_CHANTIERS")
            {
                // 1) Get TypeProduit from EcareCiments
                const string sqlType = "SELECT Type FROM EcareCiments WHERE Name = @Name;";
                string? typeProduit = await _uow.Connection.ExecuteScalarAsync<string>(
                    sqlType,
                    new { Name = r.Produit1 },
                    _uow.Transaction
                );

                // 2) Resolve PermisDeConduite
                string? permisDeConduite = null;

                // 2.1 Try from Ecare_ClientEquipements
                const string sqlPermisFromEquip = @"
                    SELECT TOP(1) PermisConducteur
                    FROM Ecare_ClientEquipements
                    WHERE ChauffeurName = @ChauffeurName;
                    "
                ;

                permisDeConduite = await _uow.Connection.ExecuteScalarAsync<string>(
                    sqlPermisFromEquip,
                    new { ChauffeurName = r.Chauffeur },
                    _uow.Transaction
                );

                // 2.2 If not found, try from Ecare_Driver
                if (string.IsNullOrWhiteSpace(permisDeConduite))
                {
                    const string sqlPermisFromDriver = @"
                        SELECT TOP(1) Permis
                        FROM Ecare_Driver
                        WHERE Nom_Complet = @NomComplet;
                        "
                    ;

                    permisDeConduite = await _uow.Connection.ExecuteScalarAsync<string>(
                        sqlPermisFromDriver,
                        new { NomComplet = r.Chauffeur },
                        _uow.Transaction
                    );
                }

                // If still null/empty after both queries → stays null (as requested)

                // 3) Insert full order
                const string sqlInsert = @"
                    INSERT INTO Ecare_Order_Legend
                    (ClientName, Chantier, Matricule, RFIDCard, TypeCamion, NombrePlombs,
                     Produit1, Quantite1, Produit2, Quantite2, TypeProduit,
                     BonDeCommande, SacNumber,
                     CodeSapProduit1, CodeSapProduit2,
                     CodeSapChantier, CodeSapClient,
                     ParkingAt, Step, AddedToQueueAt,
                     ChequeImg, ChauffeurName, PermisDeConduite)
                    VALUES
                    (
                        @ClientName,
                        @Chantier,
                        @Matricule,
                        @Slv,

                        -- Auto-fill TypeCamion from ClientEquipements
                        (SELECT TOP 1 Type FROM Ecare_ClientEquipements WHERE Matricule = @Matricule),

                        -- Auto-fill NombrePlombs from ClientEquipements
                        (SELECT TOP 1 PlombsNumber FROM Ecare_ClientEquipements WHERE Matricule = @Matricule),

                        @Produit1,
                        @Quantite1,
                        @Produit2,
                        @Quantite2,
                        @TypeProduit,

                        @BonDeCommande,
                        @SacNumber,

                        @CodeSapProduit1,
                        @CodeSapProduit2,

                        @CodeSapChantier,
                        @CodeSapClient,

                        @Now,
                        1,
                        @Now2,
                        @ChequeImage,
                        @Chauffeur,
                        @PermisDeConduite
                     );
                ";

                await _uow.Connection.ExecuteAsync(
                    sqlInsert,
                    new
                    {
                        r.ClientName,
                        r.Chantier,
                        r.Matricule,
                        Slv = r.Slv,
                        r.TypeCamion,       
                        r.NombrePlombs,     
                        r.Produit1,
                        r.Quantite1,
                        r.Produit2,
                        r.Quantite2,
                        TypeProduit = typeProduit,
                        r.BonDeCommande,
                        r.SacNumber,
                        r.CodeSapProduit1,
                        r.CodeSapProduit2,
                        r.CodeSapChantier,
                        r.CodeSapClient,
                        Now = DateTime.Now,
                        Now2 = DateTime.Now,
                        ChequeImage = r.ChequeImage,
                        Chauffeur = r.Chauffeur,
                        PermisDeConduite = string.IsNullOrWhiteSpace(permisDeConduite) ? null : permisDeConduite
                    },
                    _uow.Transaction
                );
            


            // 3) CALL SAP createOrder API
            bool hasSecondProduct =
                 !string.IsNullOrWhiteSpace(r.CodeSapProduit2) &&
                 r.Quantite2.HasValue &&
                 r.Quantite2 > 0;

                // ================================
                // BUILD SAP REQUEST BODY
                // ================================
                object sapBody;
                var sql = @"
                    SELECT TOP (1) ChauffeurName
                    FROM dbo.Ecare_ClientEquipements
                    WHERE CarteSLV = @RFIDCard
                    ORDER BY Id DESC;";

                var chauffeurName = await _uow.Connection.ExecuteScalarAsync<string>(
                    sql,
                    new { RFIDCard = r.Slv },
                    _uow.Transaction
                );
                if (hasSecondProduct)
                {

                    var qty1 = r.Quantite1.HasValue
                    ? Math.Round(Convert.ToDouble(r.Quantite1.Value), 2)
                    : 0.0;

                    var qty2 = r.Quantite2.HasValue
                        ? Math.Round(Convert.ToDouble(r.Quantite2.Value), 2)
                        : 0.0;
                    // ------- TWO PRODUCTS -------
                    sapBody = new
                    {
                        codeClient = r.CodeSapClient.PadLeft(10,'0'),
                        date = DateTime.Now.ToString("yyyy-MM-dd"),
                        purchNoC = r.BonDeCommande,
                        salesOrg = "MA18",

                        material = r.CodeSapProduit1.PadLeft(10, '0'),
                        material2 = r.CodeSapProduit2.PadLeft(10, '0'),

                        plant = "M108",

                        quantity = r.Quantite1,
                        quantity2 = r.Quantite2,

                        itemNumber = "000010",
                        itemNumber2 = "000020",

                        soldTo = r.CodeSapClient.PadLeft(10, '0'),
                        shipTo = r.CodeSapChantier.PadLeft(10, '0'),

                        reqDate = DateTime.Now.ToString("yyyy-MM-dd"),
                        reqQty = r.Quantite1,
                        reqQty2 = r.Quantite2,

                        behaveWhenError = "",
                        incoterms1 = "EXW",
                        incoterms2 = "DEPART",
                        intNumberAssignment = "",
                        testRun = true,
                        DriverMatricule = r.Matricule,
                        DriverName = chauffeurName
                    };
                }
                else
                {
                    var qty1 = r.Quantite1.HasValue
                       ? Math.Round(Convert.ToDouble(r.Quantite1.Value), 2)
                       : 0.0;
                    // ------- ONE PRODUCT -------
                    sapBody = new
                    {
                        codeClient = r.CodeSapClient.PadLeft(10, '0'),
                        date = DateTime.Now.ToString("yyyy-MM-dd"),
                        purchNoC = r.BonDeCommande,
                        salesOrg = "MA18",

                        material = r.CodeSapProduit1.PadLeft(10, '0'),

                        plant = "M108",

                        quantity = qty1,

                        itemNumber = "000010",
                        soldTo = r.CodeSapClient.PadLeft(10, '0'),
                        shipTo = r.CodeSapChantier.PadLeft(10, '0'),

                        reqDate = DateTime.Now.ToString("yyyy-MM-dd"),
                        reqQty = r.Quantite1,

                        behaveWhenError = "",
                        incoterms1 = "EXW",
                        incoterms2 = "DEPART",
                        intNumberAssignment = "",
                        testRun = false,
                        DriverMatricule = r.Matricule,
                        DriverName = chauffeurName
                    };
                }


                //var client = _httpClient.CreateClient();
                //var response = await client.PostAsJsonAsync(
                //    "https://app-emea-we-dssprod-dss-001.azurewebsites.net/api/SapOrders/createOrder",
                //    sapBody
                //);

                //var rawJson = await response.Content.ReadAsStringAsync();
                //_log.LogInformation("SAP RAW RESPONSE: " + rawJson);

                //if (!response.IsSuccessStatusCode)
                //{
                //    await _uow.RollbackAsync(ct);
                //    return Result<int>.Fail("SAP_HTTP_ERROR");
                //}

                //// 4) Parse SAP JSON safely
                //var sapJson = JsonDocument.Parse(rawJson).RootElement;

                //bool saved = sapJson.TryGetProperty("saved", out var savedProp)
                //    ? savedProp.GetBoolean()
                //    : false;

                //string sapOrderNumber = sapJson.TryGetProperty("salesDocument", out var docProp)
                //    ? docProp.GetString() ?? ""
                //    : "";

                //if (!saved || string.IsNullOrWhiteSpace(sapOrderNumber))
                //{
                //    await _uow.RollbackAsync(ct);
                //    return Result<int>.Fail("SAP_SAVE_FAILED");
                //}

                // 5) Save SAP order number in DB
                const string sqlUpdateSap = @"
                    UPDATE Ecare_Order_Legend
                    SET CodeSapCommande = @SapOrderNumber
                    WHERE RFIDCard = @Slv AND Step = 1;
                ";

                await _uow.Connection.ExecuteAsync(
                    sqlUpdateSap,
                    new { SapOrderNumber = "11111111111", Slv = r.Slv },
                    _uow.Transaction
                );

                await _uow.CommitAsync(ct);
                return Result<int>.Ok(1);
            }

            return Result<int>.Fail("INVALID_EVENT");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error in ProcessParkingCommand");
            await _uow.RollbackAsync(ct);
            return Result<int>.Fail("ERROR");
        }
    }
}
