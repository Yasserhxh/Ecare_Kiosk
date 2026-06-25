using Dapper;
using Ecare.Application.Commands.ProcessParking;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
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

    private const string SapCreateOrderUrl =
        "https://app-emea-we-dssprod-dss-001.azurewebsites.net/api/SapOrders/createOrder";
    private const string CreditDetailsUrl =
        "https://app-emea-we-dssprod-dss-001.azurewebsites.net/api/Client/DétailsCrédit";
    private const string TemaraCreditControlArea = "812";
    private const string CreditBlockedMessage =
        "Situation Credit SAP est Bloquée veuillez contacter le guichet commercial";

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
        var traceId = Guid.NewGuid();

        await SafeDbLogAsync(
            traceId,
            evt: r.Event ?? "NULL_EVENT",
            stage: "START",
            payload: r,
            slv: r.Slv?.ToString(),
            matricule: r.Matricule,
            clientName: r.ClientName,
            chantier: r.Chantier,
            ct: ct);

        await _uow.BeginAsync(ct);

        try
        {
            // =====================================================================
            // CASE 1 — ORDER FOUND → Update Step + ParkingAt
            // =====================================================================
            if (r.Event == "ORDER_FOUND")
            {
                await SafeDbLogAsync(traceId, r.Event, "CASE1_DB_UPDATE_BEGIN", payload: new { r.Slv }, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

                const string sql = @"
UPDATE dbo.Ecare_Order_Legend
SET Step = 1,
    ParkingAt = @Now
WHERE
    (
        @LegendId IS NOT NULL
        AND Id = @LegendId
    )
    OR
    (
        @LegendId IS NULL
        AND RFIDCard = @Slv
        AND BonDeCommande = @BonDeCommande
    );
";

                await _uow.Connection.ExecuteAsync(
                    sql,
                    new
                    {
                        LegendId = r.LegendId,
                        Slv = r.Slv,
                        Now = DateTime.Now,
                        BonDeCommande = r.BonDeCommande
                    },
                    _uow.Transaction
                );

                await SafeDbLogAsync(traceId, r.Event, "CASE1_DB_UPDATE_OK", statusCode: 200, isSuccess: true, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

                await _uow.CommitAsync(ct);
                await SafeDbLogAsync(traceId, r.Event, "COMMIT_OK", statusCode: 200, isSuccess: true, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

                return Result<int>.Ok(1);
            }

            // =====================================================================
            // CASE 2 — NO ORDER + NO CLIENT → Insert minimal order
            // =====================================================================
            if (r.Event == "NO_ORDER_NO_CLIENT" || r.ClientName.IsNullOrEmpty())
            {
                await SafeDbLogAsync(
                    traceId,
                    r.Event ?? "NO_ORDER_NO_CLIENT",
                    "CASE2_INSERT_BEGIN",
                    payload: r,
                    slv: r.Slv?.ToString(),
                    matricule: r.Matricule,
                    clientName: r.ClientName,
                    chantier: r.Chantier,
                    ct: ct);

                const string sql = @"
INSERT INTO dbo.Ecare_Order_Legend
(
    Matricule,
    RFIDCard,
    TypeCamion,
    NombrePlombs,
    ChauffeurName,
    CodeTransporteurSap,
    TransporteurName,
    PermisDeConduite,
    ParkingAt,
    Step,
    PTAC,
    TARE,
    AddedToQueueAt
)
SELECT
    @Matricule,
    @Slv,
    ce.TruckType,
    ce.PlombsNumber,
    ce.ChauffeurName,
    ce.CodeTransporteurSap,
    ce.TransporteurName,
    ce.PermisConducteur,
    @Now,
    1,
    ce.PTAC,
    ce.TARE,
    @Now2
FROM (SELECT 1 AS x) d
OUTER APPLY
(
    SELECT TOP (1)
        TruckType,
        PlombsNumber,
        ChauffeurName,
        CodeTransporteurSap,
        TransporteurName,
        PermisConducteur,
        PTAC,
        TARE
    FROM dbo.Ecare_ClientEquipements
    WHERE Matricule = @Matricule
    ORDER BY Id DESC
) ce;
";

                await _uow.Connection.ExecuteAsync(
                    sql,
                    new
                    {
                        r.Matricule,
                        Slv = r.Slv,
                        r.TypeCamion,
                        r.NombrePlombs,
                        Now = DateTime.Now,
                        Now2 = DateTime.Now,
                    },
                    _uow.Transaction
                );

                await SafeDbLogAsync(traceId, r.Event ?? "NO_ORDER_NO_CLIENT", "CASE2_INSERT_OK", statusCode: 200, isSuccess: true, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

                await _uow.CommitAsync(ct);
                await SafeDbLogAsync(traceId, r.Event ?? "NO_ORDER_NO_CLIENT", "COMMIT_OK", statusCode: 200, isSuccess: true, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

                return Result<int>.Ok(1);
            }

            // =====================================================================
            // CASE 3 — CLIENT + CHANTIER + PRODUCTS → Insert + SAP Order
            // =====================================================================
            if (r.Event == "CLIENTS_WITH_CHANTIERS" && !r.ClientName.IsNullOrEmpty())
            {
                await SafeDbLogAsync(
                    traceId,
                    r.Event,
                    "CASE3_BEGIN",
                    payload: r,
                    slv: r.Slv?.ToString(),
                    matricule: r.Matricule,
                    clientName: r.ClientName,
                    chantier: r.Chantier,
                    ct: ct);

                // 1) Get TypeProduit from EcareCiments
                const string sqlType = @"SELECT TOP(1) Type FROM dbo.EcareCiments WHERE Name = @Name;";
                string? typeProduit = await _uow.Connection.ExecuteScalarAsync<string>(
                    sqlType,
                    new { Name = r.Produit1 },
                    _uow.Transaction
                );

                await SafeDbLogAsync(traceId, r.Event, "TYPEPRODUIT_OK", payload: new { r.Produit1, typeProduit }, statusCode: 200, isSuccess: true, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

                // Ordered bags (SacNumber) must always follow the ordered quantity for SAC/PAL.
                // The parking/SAP feed sends tonnage but not a bag count, so derive it here when
                // none was supplied. PoidKg is the per-bag weight from the product master.
                const string sqlPoidKg = @"SELECT TOP(1) PoidKg FROM dbo.EcareCiments WHERE Name = @Name;";

                int? poidKg1 = await _uow.Connection.ExecuteScalarAsync<int?>(
                    sqlPoidKg, new { Name = r.Produit1 }, _uow.Transaction);

                string? typeProduit2 = null;
                int? poidKg2 = null;
                if (!string.IsNullOrWhiteSpace(r.Produit2))
                {
                    typeProduit2 = await _uow.Connection.ExecuteScalarAsync<string>(
                        sqlType, new { Name = r.Produit2 }, _uow.Transaction);
                    poidKg2 = await _uow.Connection.ExecuteScalarAsync<int?>(
                        sqlPoidKg, new { Name = r.Produit2 }, _uow.Transaction);
                }

                // Tonnage is the source of truth for SAC/PAL: always derive the bag count from the
                // ordered quantity, overriding whatever the parking/SAP feed sent (it carries tonnage,
                // not bags). Falls back to the incoming value only when PoidKg is unknown (computed = 0).
                int? effectiveSacNumber = r.SacNumber;
                var computedSacs =
                    ComputeSacsFromQuantity(r.Quantite1, typeProduit, poidKg1)
                    + ComputeSacsFromQuantity(r.Quantite2, typeProduit2, poidKg2);

                if (computedSacs > 0)
                    effectiveSacNumber = computedSacs;

                await SafeDbLogAsync(traceId, r.Event, "SACNUMBER_RESOLVED", payload: new { incoming = r.SacNumber, effectiveSacNumber, poidKg1, poidKg2 }, statusCode: 200, isSuccess: true, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

                var creditBlocked = await IsCreditBlockedAsync(r, ct);
                if (creditBlocked)
                {
                    await SafeDbLogAsync(
                        traceId,
                        r.Event,
                        "CREDIT_BLOCKED",
                        payload: new
                        {
                            r.CodeSapClient,
                            r.CodeSapProduit1,
                            r.Quantite1,
                            r.CodeSapProduit2,
                            r.Quantite2
                        },
                        statusCode: 409,
                        isSuccess: false,
                        slv: r.Slv?.ToString(),
                        matricule: r.Matricule,
                        clientName: r.ClientName,
                        chantier: r.Chantier,
                        ct: ct);

                    await _uow.RollbackAsync(ct);
                    return Result<int>.Fail(CreditBlockedMessage);
                }

                // 2) Resolve PermisDeConduite
                string? permisDeConduite = null;

                // 2.1 Try from Ecare_ClientEquipements
                const string sqlPermisFromEquip = @"
SELECT TOP(1) PermisConducteur
FROM dbo.Ecare_ClientEquipements
WHERE ChauffeurName = @ChauffeurName
ORDER BY Id DESC;
";

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
FROM dbo.Ecare_Driver
WHERE Nom_Complet = @NomComplet;
";
                    permisDeConduite = await _uow.Connection.ExecuteScalarAsync<string>(
                        sqlPermisFromDriver,
                        new { NomComplet = r.Chauffeur },
                        _uow.Transaction
                    );
                }

                await SafeDbLogAsync(
                    traceId,
                    r.Event,
                    "PERMIS_RESOLVED",
                    payload: new { r.Chauffeur, permisDeConduite = string.IsNullOrWhiteSpace(permisDeConduite) ? null : permisDeConduite },
                    statusCode: 200,
                    isSuccess: true,
                    slv: r.Slv?.ToString(),
                    matricule: r.Matricule,
                    ct: ct);

                // 3) Insert full order
                const string sqlInsert = @"
INSERT INTO dbo.Ecare_Order_Legend
(
    ClientName, Chantier, Matricule, RFIDCard, TypeCamion, NombrePlombs,
    Produit1, Quantite1, Produit2, Quantite2, TypeProduit,
    BonDeCommande, SacNumber,
    CodeSapProduit1, CodeSapProduit2,
    CodeSapChantier, CodeSapClient,
    ParkingAt, Step, AddedToQueueAt,
    ChequeImg, ChauffeurName, PermisDeConduite
)
VALUES
(
    @ClientName,
    @Chantier,
    @Matricule,
    @Slv,

    -- Auto-fill TypeCamion (latest by Id)
    (SELECT TOP 1 Type FROM dbo.Ecare_ClientEquipements WHERE Matricule = @Matricule ORDER BY Id DESC),

    -- Auto-fill NombrePlombs (latest by Id)
    (SELECT TOP 1 PlombsNumber FROM dbo.Ecare_ClientEquipements WHERE Matricule = @Matricule ORDER BY Id DESC),

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

                        r.Produit1,
                        r.Quantite1,
                        r.Produit2,
                        r.Quantite2,
                        TypeProduit = typeProduit,

                        r.BonDeCommande,
                        SacNumber = effectiveSacNumber,

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

                await SafeDbLogAsync(traceId, r.Event, "CASE3_INSERT_OK", statusCode: 200, isSuccess: true, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

                // 4) CALL SAP createOrder API
                bool hasSecondProduct =
                    !string.IsNullOrWhiteSpace(r.CodeSapProduit2) &&
                    r.Quantite2.HasValue &&
                    r.Quantite2 > 0;

                // Find chauffeur name (latest by SLV)
                const string sqlChauffeur = @"
SELECT TOP (1) ChauffeurName
FROM dbo.Ecare_ClientEquipements
WHERE CarteSLV = @RFIDCard
ORDER BY Id DESC;
";
                var chauffeurName = await _uow.Connection.ExecuteScalarAsync<string>(
                    sqlChauffeur,
                    new { RFIDCard = r.Slv },
                    _uow.Transaction
                );

                object sapBody;

                var qty1 = r.Quantite1.HasValue ? Math.Round(Convert.ToDouble(r.Quantite1.Value), 2) : 0.0;
                var qty2 = r.Quantite2.HasValue ? Math.Round(Convert.ToDouble(r.Quantite2.Value), 2) : 0.0;

                if (hasSecondProduct)
                {
                    sapBody = new
                    {
                        codeClient = (r.CodeSapClient ?? "").PadLeft(10, '0'),
                        date = DateTime.Now.ToString("yyyy-MM-dd"),
                        purchNoC = r.BonDeCommande,
                        salesOrg = "MA18",

                        material = (r.CodeSapProduit1 ?? "").PadLeft(10, '0'),
                        material2 = (r.CodeSapProduit2 ?? "").PadLeft(10, '0'),

                        plant = "M108",

                        quantity = qty1,
                        quantity2 = qty2,

                        itemNumber = "000010",
                        itemNumber2 = "000020",

                        soldTo = (r.CodeSapClient ?? "").PadLeft(10, '0'),
                        shipTo = (r.CodeSapChantier ?? "").PadLeft(10, '0'),

                        reqDate = DateTime.Now.ToString("yyyy-MM-dd"),
                        reqQty = qty1,
                        reqQty2 = qty2,

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
                    sapBody = new
                    {
                        codeClient = (r.CodeSapClient ?? "").PadLeft(10, '0'),
                        date = DateTime.Now.ToString("yyyy-MM-dd"),
                        purchNoC = r.BonDeCommande,
                        salesOrg = "MA18",

                        material = (r.CodeSapProduit1 ?? "").PadLeft(10, '0'),

                        plant = "M108",

                        quantity = qty1,

                        itemNumber = "000010",
                        soldTo = (r.CodeSapClient ?? "").PadLeft(10, '0'),
                        shipTo = (r.CodeSapChantier ?? "").PadLeft(10, '0'),

                        reqDate = DateTime.Now.ToString("yyyy-MM-dd"),
                        reqQty = qty1,

                        behaveWhenError = "",
                        incoterms1 = "EXW",
                        incoterms2 = "DEPART",
                        intNumberAssignment = "",
                        testRun = false,

                        DriverMatricule = r.Matricule,
                        DriverName = chauffeurName
                    };
                }

                await SafeDbLogAsync(
                    traceId,
                    r.Event,
                    "SAP_REQUEST",
                    payload: sapBody,
                    url: SapCreateOrderUrl,
                    httpMethod: "POST",
                    slv: r.Slv?.ToString(),
                    matricule: r.Matricule,
                    clientName: r.ClientName,
                    chantier: r.Chantier,
                    ct: ct);

                var sw = Stopwatch.StartNew();
                var client = _httpClient.CreateClient();
                var response = await client.PostAsJsonAsync(SapCreateOrderUrl, sapBody, ct);
                var rawJson = await response.Content.ReadAsStringAsync(ct);
                sw.Stop();

                _log.LogInformation("SAP RAW RESPONSE: {Raw}", rawJson);

                await SafeDbLogAsync(
                    traceId,
                    r.Event,
                    "SAP_RESPONSE",
                    statusCode: (int)response.StatusCode,
                    isSuccess: response.IsSuccessStatusCode,
                    elapsedMs: (int)sw.ElapsedMilliseconds,
                    responseBody: rawJson,
                    url: SapCreateOrderUrl,
                    httpMethod: "POST",
                    slv: r.Slv?.ToString(),
                    matricule: r.Matricule,
                    clientName: r.ClientName,
                    chantier: r.Chantier,
                    ct: ct);

                if (!response.IsSuccessStatusCode)
                {
                    await SafeDbLogAsync(traceId, r.Event, "SAP_HTTP_ERROR", statusCode: (int)response.StatusCode, isSuccess: false, responseBody: rawJson, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

                    await _uow.RollbackAsync(ct);
                    await SafeDbLogAsync(traceId, r.Event, "ROLLBACK_OK", statusCode: 500, isSuccess: false, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

                    return Result<int>.Fail("SAP_HTTP_ERROR");
                }

                // 5) Parse SAP JSON safely
                JsonElement root;
                try
                {
                    root = JsonDocument.Parse(rawJson).RootElement;
                }
                catch (Exception parseEx)
                {
                    await SafeDbLogAsync(traceId, r.Event, "SAP_PARSE_EXCEPTION", payload: sapBody, isSuccess: false, responseBody: rawJson, ex: parseEx, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

                    await _uow.RollbackAsync(ct);
                    await SafeDbLogAsync(traceId, r.Event, "ROLLBACK_OK", statusCode: 500, isSuccess: false, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

                    return Result<int>.Fail("SAP_PARSE_ERROR");
                }

                bool saved = root.TryGetProperty("saved", out var savedProp) && savedProp.ValueKind == JsonValueKind.True;
                string sapOrderNumber =
                    root.TryGetProperty("salesDocument", out var docProp) ? (docProp.GetString() ?? "") : "";

                if (!saved || string.IsNullOrWhiteSpace(sapOrderNumber))
                {
                    await SafeDbLogAsync(
                        traceId,
                        r.Event,
                        "SAP_SAVE_FAILED",
                        payload: new { saved, sapOrderNumber },
                        statusCode: 400,
                        isSuccess: false,
                        responseBody: rawJson,
                        slv: r.Slv?.ToString(),
                        matricule: r.Matricule,
                        ct: ct);

                    await _uow.RollbackAsync(ct);
                    await SafeDbLogAsync(traceId, r.Event, "ROLLBACK_OK", statusCode: 500, isSuccess: false, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

                    return Result<int>.Fail("SAP_SAVE_FAILED");
                }

                // 6) Save SAP order number in DB (latest row only)
                const string sqlUpdateSap = @"
;WITH LastRow AS
(
    SELECT TOP (1) *
    FROM dbo.Ecare_Order_Legend
    WHERE RFIDCard = @Slv AND Step = 1
    ORDER BY Id DESC
)
UPDATE LastRow
SET CodeSapCommande = @SapOrderNumber;
";

                await _uow.Connection.ExecuteAsync(
                    sqlUpdateSap,
                    new { SapOrderNumber = sapOrderNumber, Slv = r.Slv },
                    _uow.Transaction
                );

                await SafeDbLogAsync(
                    traceId,
                    r.Event,
                    "SAP_DB_UPDATE_OK",
                    payload: new { sapOrderNumber },
                    statusCode: 200,
                    isSuccess: true,
                    slv: r.Slv?.ToString(),
                    matricule: r.Matricule,
                    ct: ct);

                await _uow.CommitAsync(ct);
                await SafeDbLogAsync(traceId, r.Event, "COMMIT_OK", statusCode: 200, isSuccess: true, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

                return Result<int>.Ok(1);
            }

            await SafeDbLogAsync(
                traceId,
                r.Event ?? "NULL_EVENT",
                "INVALID_EVENT",
                payload: r,
                statusCode: 400,
                isSuccess: false,
                slv: r.Slv?.ToString(),
                matricule: r.Matricule,
                clientName: r.ClientName,
                chantier: r.Chantier,
                ct: ct);

            await _uow.RollbackAsync(ct);
            await SafeDbLogAsync(traceId, r.Event ?? "NULL_EVENT", "ROLLBACK_OK", statusCode: 400, isSuccess: false, slv: r.Slv?.ToString(), matricule: r.Matricule, ct: ct);

            return Result<int>.Fail("INVALID_EVENT");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error in ProcessParkingCommand");

            await SafeDbLogAsync(
                traceId,
                command?.Request?.Event ?? "UNKNOWN",
                "EXCEPTION",
                payload: command?.Request,
                statusCode: 500,
                isSuccess: false,
                ex: ex,
                slv: command?.Request?.Slv?.ToString(),
                matricule: command?.Request?.Matricule,
                clientName: command?.Request?.ClientName,
                chantier: command?.Request?.Chantier,
                ct: ct);

            await _uow.RollbackAsync(ct);
            await SafeDbLogAsync(traceId, command?.Request?.Event ?? "UNKNOWN", "ROLLBACK_OK", statusCode: 500, isSuccess: false, slv: command?.Request?.Slv?.ToString(), matricule: command?.Request?.Matricule, ct: ct);

            return Result<int>.Fail("ERROR");
        }
    }

    private async Task<bool> IsCreditBlockedAsync(ParkingProcessRequest request, CancellationToken ct)
    {
        var codeClient = request.CodeSapClient?.Trim();
        if (string.IsNullOrWhiteSpace(codeClient))
            return true;

        var estimatedAmount = await CalculateEstimatedOrderAmountAsync(request, ct);

        var client = _httpClient.CreateClient();
        using var response = await client.PostAsJsonAsync(
            CreditDetailsUrl,
            new
            {
                Entreprise = TemaraCreditControlArea,
                CodeClient = codeClient
            },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            _log.LogWarning(
                "Credit check failed for parking order. CodeClient={CodeClient}, Status={Status}",
                codeClient,
                response.StatusCode);
            return true;
        }

        var rawJson = await response.Content.ReadAsStringAsync(ct);
        using var document = JsonDocument.Parse(rawJson);
        var root = document.RootElement;

        var accountStatus = ReadString(root, "Statut_Du_Compte", "statut_Du_Compte", "StatutDuCompte", "statutDuCompte");
        if (!string.IsNullOrWhiteSpace(accountStatus) &&
            accountStatus.Contains("blo", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var plafond = ReadDecimal(root, "Plafond", "plafond") ?? 0m;
        var enCours = ReadDecimal(root, "EnCours", "enCours") ?? 0m;
        var availableCredit = plafond - enCours;

        return estimatedAmount > availableCredit;
    }

    private async Task<decimal> CalculateEstimatedOrderAmountAsync(ParkingProcessRequest request, CancellationToken ct)
    {
        var codes = new[]
            {
                request.CodeSapProduit1?.Trim(),
                request.CodeSapProduit2?.Trim()
            }
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (codes.Length == 0)
            return 0m;

        const string sql = @"
SELECT
    CodeSAP,
    TarifParTonne
FROM dbo.EcareCiments
WHERE CodeSAP IN @Codes;
";

        var rows = await _uow.Connection.QueryAsync<CementTariffRow>(
            new CommandDefinition(
                sql,
                new { Codes = codes },
                transaction: _uow.Transaction,
                cancellationToken: ct));

        var tariffs = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.CodeSAP))
            .ToDictionary(row => row.CodeSAP!.Trim(), row => row.TarifParTonne, StringComparer.OrdinalIgnoreCase);

        return GetLineAmount(request.CodeSapProduit1, request.Quantite1, tariffs)
             + GetLineAmount(request.CodeSapProduit2, request.Quantite2, tariffs);
    }

    // Ordered bags for one product line: ceil(quantityTons * 1000 / bagWeightKg), only for SAC/PAL.
    // Returns 0 when not applicable (not SAC/PAL, no quantity, or no bag weight) so callers can sum lines.
    private static int ComputeSacsFromQuantity(decimal? quantiteTonnes, string? typeProduit, int? poidKg)
    {
        if (string.IsNullOrWhiteSpace(typeProduit))
            return 0;

        var isSacOrPal =
            typeProduit.Contains("SAC", StringComparison.OrdinalIgnoreCase) ||
            typeProduit.Contains("PAL", StringComparison.OrdinalIgnoreCase);

        if (!isSacOrPal ||
            !quantiteTonnes.HasValue || quantiteTonnes.Value <= 0 ||
            !poidKg.HasValue || poidKg.Value <= 0)
            return 0;

        return (int)Math.Ceiling((quantiteTonnes.Value * 1000m) / poidKg.Value);
    }

    private static decimal GetLineAmount(string? codeSap, decimal? quantity, IReadOnlyDictionary<string, decimal?> tariffs)
    {
        if (string.IsNullOrWhiteSpace(codeSap) || !quantity.HasValue || quantity.Value <= 0)
            return 0m;

        return tariffs.TryGetValue(codeSap.Trim(), out var unitPrice) && unitPrice.HasValue
            ? quantity.Value * unitPrice.Value
            : 0m;
    }

    private static decimal? ReadDecimal(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String &&
                decimal.TryParse(value.GetString(), out var parsed))
                return parsed;
        }

        return null;
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }

    private sealed class CementTariffRow
    {
        public string? CodeSAP { get; set; }
        public decimal? TarifParTonne { get; set; }
    }

    // ----------------------------
    // DB LOGGING (survives rollback)
    // ----------------------------

    private static string? ToJson(object? obj)
    {
        if (obj is null) return null;

        return JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    private static string? Trunc(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= max ? s : s.Substring(0, max);
    }

    // Uses new SqlConnection so logs are committed even if _uow rolls back
    private async Task SafeDbLogAsync(
        Guid traceId,
        string evt,
        string stage,
        object? payload = null,
        int? statusCode = null,
        bool? isSuccess = null,
        int? elapsedMs = null,
        string? url = null,
        string? httpMethod = null,
        string? responseBody = null,
        Exception? ex = null,
        string? slv = null,
        string? matricule = null,
        string? clientName = null,
        string? chantier = null,
        CancellationToken ct = default)
    {
        try
        {
            var connStr = _cfg.GetConnectionString("SqlServer");
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);

            const string sql = @"
INSERT INTO dbo.Ecare_AppLogs
(
    Source, Handler, Hostname, Environment, AppVersion,
    TraceId, Event, Stage,
    Slv, Matricule, ClientName, Chantier,
    Url, HttpMethod, StatusCode, IsSuccess, ElapsedMs,
    PayloadJson, ResponseBody,
    ExceptionType, ExceptionMessage, ExceptionStackTrace
)
VALUES
(
    @Source, @Handler, @Hostname, @Environment, @AppVersion,
    @TraceId, @Event, @Stage,
    @Slv, @Matricule, @ClientName, @Chantier,
    @Url, @HttpMethod, @StatusCode, @IsSuccess, @ElapsedMs,
    @PayloadJson, @ResponseBody,
    @ExceptionType, @ExceptionMessage, @ExceptionStackTrace
);
";

            // keep DB usable: truncate very large payload/response
            var payloadJson = Trunc(ToJson(payload), 50_000);
            var resp = Trunc(responseBody, 50_000);

            await conn.ExecuteAsync(sql, new
            {
                Source = "Ecare",
                Handler = nameof(ProcessParkingCommandHandler),
                Hostname = Environment.MachineName,
                Environment = _cfg["ASPNETCORE_ENVIRONMENT"],
                AppVersion = typeof(ProcessParkingCommandHandler).Assembly.GetName().Version?.ToString(),

                TraceId = traceId,
                Event = evt,
                Stage = stage,

                Slv = slv,
                Matricule = matricule,
                ClientName = clientName,
                Chantier = chantier,

                Url = url,
                HttpMethod = httpMethod,
                StatusCode = statusCode,
                IsSuccess = isSuccess,
                ElapsedMs = elapsedMs,

                PayloadJson = payloadJson,
                ResponseBody = resp,

                ExceptionType = ex?.GetType().FullName,
                ExceptionMessage = Trunc(ex?.Message, 4000),
                ExceptionStackTrace = ex?.ToString()
            });
        }
        catch (Exception logEx)
        {
            // never break business flow because of logging
            _log.LogWarning(logEx, "Failed to write dbo.Ecare_AppLogs");
        }
    }
}
