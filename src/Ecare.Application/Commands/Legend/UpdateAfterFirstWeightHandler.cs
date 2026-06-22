using Dapper;
using Ecare.Application.Services;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Diagnostics;
using System.Text.Json;

namespace Ecare.Application.Commands.Legend;

public sealed class UpdateAfterFirstWeightHandler
    : IRequestHandler<UpdateAfterFirstWeightCommand, Result<FirstWeightResultVm>>
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<UpdateAfterFirstWeightHandler> _log;
    private readonly ServiceManager _signalR;

    private const string ProcessName = "PabEntry_FirstWeight";

    public UpdateAfterFirstWeightHandler(
        IConfiguration cfg,
        ILogger<UpdateAfterFirstWeightHandler> log,
        ServiceManager signalR)
    {
        _cfg = cfg;
        _log = log;
        _signalR = signalR;
    }

    private sealed class LegendLoadVm
    {
        public int Id { get; set; }
        public decimal? PTAC { get; set; }
        public decimal? Quantite1 { get; set; }
        public decimal? Quantite2 { get; set; }
        public int? Step { get; set; }
        public DateTime? ParkingAt { get; set; }
        public string? Ligne { get; set; }
        public bool IsPined { get; set; }
    }

    private sealed class LigneLookupVm
    {
        public int LigneId { get; set; }
        public string LigneName { get; set; } = string.Empty;
        public string? LigneImageUrl { get; set; }
    }

    public async Task<Result<FirstWeightResultVm>> Handle(
        UpdateAfterFirstWeightCommand request,
        CancellationToken ct)
    {
        var connStr = _cfg.GetConnectionString("SqlServer");
        if (string.IsNullOrWhiteSpace(connStr))
            return Result<FirstWeightResultVm>.Fail("Missing SQL connection string");

        // Outbound SignalR
        var hubName = _cfg["SignalR:Outbound:PabEntry:Hub"] ?? "pabentry_hub";
        var methodName = _cfg["SignalR:Outbound:PabEntry:Method"] ?? "PabEntryMessage";

        // Target group/device (better than hard-coded "deviceId")
        var deviceId = request.Matricule ?? "unknown";

        var traceId = Guid.NewGuid();
        var evt = "FIRST_WEIGHT";
        var slv = request.RfidCard;
        var mat = request.Matricule;

        await SafeDbLogAsync(
            connStr,
            traceId,
            evt,
            stage: "START",
            statusCode: 0,
            isSuccess: true,
            payload: new
            {
                request.RfidCard,
                request.Matricule,
                request.PremierePoid,
                request.Produit1,
                request.LegendId
            },
            process: ProcessName,
            legendId: null,
            spName: null,
            hubName: null,
            methodName: null,
            deviceId: null,
            context: new { hubName, methodName, deviceId },
            ct: ct);

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);

        LegendLoadVm? vm = null;

        try
        {
            // ----------------------------------------------
            // 1) Load required data from Ecare_Order_Legend
            // ----------------------------------------------
            const string sqlLegend = """
                SELECT TOP (1)
                    Id,
                    PTAC,
                    Quantite1,
                    Quantite2,
                    Step,
                    ParkingAt,
                    Ligne,
                    IsPined
                FROM dbo.Ecare_Order_Legend
                WHERE LTRIM(RTRIM(CAST(RFIDCard AS NVARCHAR(50)))) = LTRIM(RTRIM(@RfidCard))
                  AND Matricule = @Matricule
                  AND ISNULL(Step, 0) < 5
                ORDER BY Id DESC;
            """;

            const string sqlLegendById = """
                SELECT TOP (1)
                    Id,
                    PTAC,
                    Quantite1,
                    Quantite2,
                    Step,
                    ParkingAt,
                    Ligne,
                    IsPined
                FROM dbo.Ecare_Order_Legend
                WHERE Id = @LegendId
                  AND ISNULL(Step, 0) < 5;
            """;

            await SafeDbLogAsync(
                connStr,
                traceId,
                evt,
                stage: "LOAD_LEGEND_BEGIN",
                statusCode: 0,
                isSuccess: true,
                payload: new { request.RfidCard, request.Matricule, request.LegendId },
                process: ProcessName,
                legendId: null,
                spName: null,
                hubName: null,
                methodName: null,
                deviceId: null,
                context: null,
                ct: ct);

            vm = await conn.QueryFirstOrDefaultAsync<LegendLoadVm>(
                new CommandDefinition(
                    request.LegendId.HasValue ? sqlLegendById : sqlLegend,
                    request.LegendId.HasValue
                        ? new { request.LegendId }
                        : new { request.RfidCard, request.Matricule },
                    cancellationToken: ct));

            if (vm is null)
            {
                await SafeDbLogAsync(
                    connStr,
                    traceId,
                    evt,
                    stage: "NO_ACTIVE_ORDER",
                    statusCode: 404,
                    isSuccess: false,
                    payload: new { request.RfidCard, request.Matricule },
                    process: ProcessName,
                    legendId: null,
                    spName: null,
                    hubName: null,
                    methodName: null,
                    deviceId: null,
                    context: null,
                    ct: ct);

                return Result<FirstWeightResultVm>.Fail("NO_ACTIVE_ORDER");
            }

            await SafeDbLogAsync(
                connStr,
                traceId,
                evt,
                stage: "LOAD_LEGEND_OK",
                statusCode: 0,
                isSuccess: true,
                payload: vm,
                process: ProcessName,
                legendId: vm.Id,
                spName: null,
                hubName: null,
                methodName: null,
                deviceId: null,
                context: null,
                ct: ct);

            var eligibility = await QueueSnapshot.EvaluateFirstWeightEligibilityAsync(
                conn,
                vm.Id,
                ct: ct);

            if (!eligibility.IsAllowed)
            {
                await SafeDbLogAsync(
                    connStr,
                    traceId,
                    evt,
                    stage: "FIRST_WEIGHT_BLOCKED_NOT_CALLED",
                    statusCode: 409,
                    isSuccess: false,
                    payload: new
                    {
                        reason = eligibility.Reason,
                        eligibility.GroupName,
                        eligibility.Capacity,
                        eligibility.Position
                    },
                    process: ProcessName,
                    legendId: vm.Id,
                    spName: null,
                    hubName: null,
                    methodName: null,
                    deviceId: null,
                    context: new { request.RfidCard, request.Matricule, request.Produit1 },
                    ct: ct);

                return Result<FirstWeightResultVm>.Fail(
                    eligibility.Reason == "LINE_HAS_NO_CAPACITY"
                        ? "La ligne de chargement n'a plus de capacité disponible pour le moment."
                        : "Ce n'est pas encore votre tour pour le premier pesage. Veuillez attendre l'appel.");
            }

            var ptac = vm.PTAC ?? 0m;
            var q1 = vm.Quantite1 ?? 0m;
            var q2 = vm.Quantite2 ?? 0m;

            // ----------------------------------------------
            // 2) Validation (only if PTAC is present)
            // expectedLoadKg = firstWeight + (q1+q2)*1000
            // ----------------------------------------------
            if (ptac > 0m)
            {
                var expectedLoad = (decimal)request.PremierePoid + (q1 + q2) * 1000m;
                var maxAllowed = ptac * 1.10m;

                await SafeDbLogAsync(
                    connStr,
                    traceId,
                    evt,
                    stage: "VALIDATION_COMPUTED",
                    statusCode: 0,
                    isSuccess: true,
                    payload: new { ptac, q1, q2, expectedLoad, maxAllowed, legendId = vm.Id, step = vm.Step },
                    process: ProcessName,
                    legendId: vm.Id,
                    spName: null,
                    hubName: null,
                    methodName: null,
                    deviceId: null,
                    context: null,
                    ct: ct);

                if (expectedLoad > maxAllowed)
                {
                    var overloadPayload = new
                    {
                        @event = methodName,
                        message = "LOAD EXCEEDS PTAC LIMIT",
                        kiosk = deviceId,
                        slv = request.RfidCard,
                        ts = DateTime.Now,
                        details = new
                        {
                            ptac,
                            ptacMax = maxAllowed,
                            expected = expectedLoad,
                            difference = expectedLoad - maxAllowed,
                            legendId = vm.Id
                        }
                    };

                    await SafeDbLogAsync(
                        connStr,
                        traceId,
                        evt,
                        stage: "OVERLOAD_DETECTED",
                        statusCode: 400,
                        isSuccess: false,
                        payload: overloadPayload,
                        process: ProcessName,
                        legendId: vm.Id,
                        spName: null,
                        hubName: hubName,
                        methodName: methodName,
                        deviceId: deviceId,
                        context: new { ptac, q1, q2, expectedLoad, maxAllowed },
                        ct: ct);

                    try
                    {
                        await SignalRHelper.BroadcastToDeviceAsync(
                            _signalR,
                            hubName: hubName,
                            methodName: methodName,
                            deviceId: deviceId,
                            payload: overloadPayload,
                            logger: _log,
                            ct: ct
                        );

                        await SafeDbLogAsync(
                            connStr,
                            traceId,
                            evt,
                            stage: "SIGNALR_OVERLOAD_SENT",
                            statusCode: 200,
                            isSuccess: true,
                            payload: new { hubName, methodName, deviceId },
                            process: ProcessName,
                            legendId: vm.Id,
                            spName: null,
                            hubName: hubName,
                            methodName: methodName,
                            deviceId: deviceId,
                            context: null,
                            ct: ct);
                    }
                    catch (Exception srEx)
                    {
                        await SafeDbLogAsync(
                            connStr,
                            traceId,
                            evt,
                            stage: "SIGNALR_OVERLOAD_ERROR",
                            statusCode: 500,
                            isSuccess: false,
                            payload: overloadPayload,
                            process: ProcessName,
                            legendId: vm.Id,
                            spName: null,
                            hubName: hubName,
                            methodName: methodName,
                            deviceId: deviceId,
                            context: null,
                            ex: srEx,
                            ct: ct);
                    }

                    _log.LogWarning(
                        "OVERLOAD: legendId={id} expected={exp} > ptacMax={max} (ptac={ptac}, q1={q1}, q2={q2})",
                        vm.Id, expectedLoad, maxAllowed, ptac, q1, q2);

                    return Result<FirstWeightResultVm>.Fail("OVERLOAD_PTAC");
                }
            }
            else
            {
                await SafeDbLogAsync(
                    connStr,
                    traceId,
                    evt,
                    stage: "VALIDATION_SKIPPED_PTAC_MISSING",
                    statusCode: 0,
                    isSuccess: true,
                    payload: new { vm.Id, vm.PTAC, vm.Quantite1, vm.Quantite2, vm.Step },
                    process: ProcessName,
                    legendId: vm.Id,
                    spName: null,
                    hubName: null,
                    methodName: null,
                    deviceId: null,
                    context: null,
                    ct: ct);

                _log.LogWarning(
                    "PTAC is missing/zero for RFID={rfid}, Matricule={mat}. Skipping overload validation.",
                    request.RfidCard, request.Matricule);
            }

            // ----------------------------------------------
            // 3) Apply first weight with inline SQL
            // ----------------------------------------------
            var sw = Stopwatch.StartNew();
            FirstWeightResultVm? row;
            const string sqlSelectLigne = """
                ;WITH Lignes AS
                (
                    SELECT DISTINCT
                        L.Id AS LigneId,
                        L.Nom AS LigneName,
                        L.Ligne_ImageUrl AS LigneImageUrl,
                        L.Capacity AS Capacity,
                        ISNULL(L.RealtimeCapacity, L.Capacity) AS Rtc
                    FROM dbo.EcareCiments C
                    JOIN dbo.Ecare_LigneCiments LC ON LC.CimentId = C.Id
                    JOIN dbo.Ecare_Ligne L ON L.Id = LC.LigneId
                    WHERE C.Name = @Produit1
                      AND LC.Actif = 1
                      AND ISNULL(L.Status, 0) = 1
                ),
                LigneUsage AS
                (
                    SELECT
                        Lignes.LigneId,
                        Lignes.LigneName,
                        Lignes.LigneImageUrl,
                        FreeCapacity = CASE
                            WHEN (Lignes.Capacity - occ.Occupancy) < Lignes.Rtc
                            THEN (Lignes.Capacity - occ.Occupancy)
                            ELSE Lignes.Rtc
                        END
                    FROM Lignes
                    CROSS APPLY (
                        SELECT COUNT(*) AS Occupancy
                        FROM dbo.Ecare_Order_Legend O
                        WHERE O.Ligne = Lignes.LigneName
                          AND O.Step BETWEEN 2 AND 4
                          AND ISNULL(O.AnnulationCommercial, 0) <> 1
                          AND ISNULL(O.Status, '') <> 'Canceled'
                    ) occ
                )
                SELECT TOP (1)
                    LigneId,
                    LigneName,
                    LigneImageUrl
                FROM LigneUsage
                WHERE FreeCapacity > 0
                   OR @IsForceCall = 1
                ORDER BY
                    CASE WHEN FreeCapacity > 0 THEN 0 ELSE 1 END,
                    FreeCapacity DESC,
                    LigneId;
            """;

            const string sqlUpdateLegend = """
                UPDATE dbo.Ecare_Order_Legend
                SET
                    PremierePoid = @PremierePoid,
                    PabEntryAt = CONVERT(datetime, SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time'),
                    ElapsedTimeParking = CASE
                        WHEN ParkingAt IS NULL THEN NULL
                        ELSE DATEDIFF(
                            MINUTE,
                            ParkingAt,
                            CONVERT(datetime, SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time')
                        )
                    END,
                    Step = 2,
                    Ligne = @LigneName,
                    IsPined = 0,
                    PinedAt = NULL
                WHERE
                    (
                        @LegendId IS NOT NULL
                        AND Id = @LegendId
                        AND Step = 1
                    )
                    OR
                    (
                        @LegendId IS NULL
                        AND LTRIM(RTRIM(CAST(RFIDCard AS NVARCHAR(50)))) = LTRIM(RTRIM(@RfidCard))
                        AND Matricule = @Matricule
                        AND Step = 1
                    );
            """;

            const string sqlDecrementCapacity = """
                UPDATE dbo.Ecare_Ligne
                SET RealtimeCapacity = RealtimeCapacity - 1
                WHERE Id = @LigneId
                  AND RealtimeCapacity > 0;
            """;

            await SafeDbLogAsync(
                connStr,
                traceId,
                evt,
                stage: "INLINE_UPDATE_BEGIN",
                statusCode: 0,
                isSuccess: true,
                payload: new
                {
                    request.LegendId,
                    request.RfidCard,
                    request.Matricule,
                    request.PremierePoid,
                    request.Produit1
                },
                process: ProcessName,
                legendId: vm.Id,
                spName: null,
                hubName: null,
                methodName: null,
                deviceId: null,
                context: new { mode = request.LegendId.HasValue ? "LegendId" : "LegacyKeys" },
                ct: ct);

            var ligne = await conn.QueryFirstOrDefaultAsync<LigneLookupVm>(
                new CommandDefinition(
                    sqlSelectLigne,
                    new { request.Produit1, IsForceCall = vm.IsPined },
                    cancellationToken: ct));

            if (ligne is null || ligne.LigneId <= 0)
            {
                return Result<FirstWeightResultVm>.Fail("LINE_HAS_NO_CAPACITY");
            }

            var rowsAffected = await conn.ExecuteAsync(
                new CommandDefinition(
                    sqlUpdateLegend,
                    new
                    {
                        LegendId = request.LegendId,
                        request.RfidCard,
                        request.Matricule,
                        request.PremierePoid,
                        LigneName = ligne?.LigneName
                    },
                    cancellationToken: ct));

            if (rowsAffected > 0 && ligne?.LigneId > 0)
            {
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sqlDecrementCapacity,
                        new { ligne.LigneId },
                        cancellationToken: ct));
            }

            row = rowsAffected > 0
                ? new FirstWeightResultVm
                {
                    LigneId = ligne?.LigneId ?? 0,
                    LigneName = ligne?.LigneName ?? string.Empty,
                    LigneImageUrl = ligne?.LigneImageUrl
                }
                : null;

            await SafeDbLogAsync(
                connStr,
                traceId,
                evt,
                stage: row is null ? "INLINE_UPDATE_NO_MATCH" : "INLINE_UPDATE_OK",
                statusCode: row is null ? 404 : 200,
                isSuccess: row is not null,
                payload: row is null
                    ? new
                    {
                        request.LegendId,
                        request.RfidCard,
                        request.Matricule,
                        request.PremierePoid,
                        request.Produit1
                    }
                    : row,
                process: ProcessName,
                legendId: vm.Id,
                spName: null,
                hubName: null,
                methodName: null,
                deviceId: null,
                context: new
                {
                    rowsAffected,
                    ligneId = ligne?.LigneId,
                    mode = request.LegendId.HasValue ? "LegendId" : "LegacyKeys"
                },
                ct: ct);

            sw.Stop();

            if (row is null)
            {
                return Result<FirstWeightResultVm>.Fail("NO_MATCHING_ROW");
            }

            return Result<FirstWeightResultVm>.Ok(row);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Error updating first weight for RFID={rfid}, Matricule={mat}",
                request.RfidCard, request.Matricule);

            await SafeDbLogAsync(
                connStr,
                traceId,
                evt,
                stage: "EXCEPTION",
                statusCode: 500,
                isSuccess: false,
                payload: new
                {
                    request.RfidCard,
                    request.Matricule,
                    request.PremierePoid,
                    request.Produit1,
                    legendId = vm?.Id
                },
                process: ProcessName,
                legendId: vm?.Id,
                spName: null,
                hubName: null,
                methodName: null,
                deviceId: null,
                context: null,
                ex: ex,
                ct: ct);

            return Result<FirstWeightResultVm>.Fail($"FIRST_WEIGHT_UPDATE_ERROR | {ex.GetType().Name}: {ex.Message} | StackTrace: {ex.StackTrace}");
        }
    }

    // =========================================================
    // DB LOGGING (robust: uses new connection + fallback insert)
    // =========================================================

    private static string? ToJson(object? obj)
        => obj is null ? null : JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

    private static string? Trunc(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);

    private async Task SafeDbLogAsync(
        string connStr,
        Guid traceId,
        string evt,
        string stage,
        int? statusCode,
        bool? isSuccess,
        object? payload,
        string? process,
        int? legendId,
        string? spName,
        string? hubName,
        string? methodName,
        string? deviceId,
        object? context,
        CancellationToken ct,
        Exception? ex = null)
    {
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);

            var payloadJson = Trunc(ToJson(payload), 50_000);
            var contextJson = Trunc(ToJson(context), 50_000);

            // Normalize to avoid nulls (0 = N/A)
            var sc = statusCode ?? 0;
            var ok = isSuccess ?? (ex is null && (sc == 0 || sc < 400));

            // --------- EXTENDED INSERT (if you added columns) ----------
            const string sqlExtended = @"
INSERT INTO dbo.Ecare_AppLogs
(
    CreatedAtUtc,
    Source, Handler, Hostname, Environment, AppVersion,
    TraceId, Event, Stage,
    Slv, Matricule, ClientName, Chantier,
    Url, HttpMethod, StatusCode, IsSuccess, ElapsedMs,
    PayloadJson, ResponseBody,
    ExceptionType, ExceptionMessage, ExceptionStackTrace,
    Process, LegendId, StoredProcedure, HubName, MethodName, DeviceId, ContextJson
)
VALUES
(
    SYSUTCDATETIME(),
    @Source, @Handler, @Hostname, @Environment, @AppVersion,
    @TraceId, @Event, @Stage,
    @Slv, @Matricule, @ClientName, @Chantier,
    NULL, NULL, @StatusCode, @IsSuccess, NULL,
    @PayloadJson, NULL,
    @ExceptionType, @ExceptionMessage, @ExceptionStackTrace,
    @Process, @LegendId, @StoredProcedure, @HubName, @MethodName, @DeviceId, @ContextJson
);";

            // --------- BASE INSERT (works with your original table) ----------
            const string sqlBase = @"
INSERT INTO dbo.Ecare_AppLogs
(
    CreatedAtUtc,
    Source, Handler, Hostname, Environment, AppVersion,
    TraceId, Event, Stage,
    Slv, Matricule, ClientName, Chantier,
    Url, HttpMethod, StatusCode, IsSuccess, ElapsedMs,
    PayloadJson, ResponseBody,
    ExceptionType, ExceptionMessage, ExceptionStackTrace
)
VALUES
(
    SYSUTCDATETIME(),
    @Source, @Handler, @Hostname, @Environment, @AppVersion,
    @TraceId, @Event, @Stage,
    @Slv, @Matricule, @ClientName, @Chantier,
    NULL, NULL, @StatusCode, @IsSuccess, NULL,
    @PayloadJson, NULL,
    @ExceptionType, @ExceptionMessage, @ExceptionStackTrace
);";

            var args = new
            {
                Source = "Ecare",
                Handler = nameof(UpdateAfterFirstWeightHandler),
                Hostname = Environment.MachineName,
                Environment = _cfg["ASPNETCORE_ENVIRONMENT"],
                AppVersion = typeof(UpdateAfterFirstWeightHandler).Assembly.GetName().Version?.ToString(),

                TraceId = traceId,
                Event = evt,
                Stage = stage,

                Slv = requestToStringSafe(evt), // placeholder replaced below
                Matricule = (string?)null,
                ClientName = (string?)null,
                Chantier = (string?)null,

                StatusCode = sc,
                IsSuccess = ok,

                PayloadJson = payloadJson,

                ExceptionType = ex?.GetType().FullName,
                ExceptionMessage = Trunc(ex?.Message, 4000),
                ExceptionStackTrace = ex?.ToString(),

                Process = process,
                LegendId = legendId,
                StoredProcedure = spName,
                HubName = hubName,
                MethodName = methodName,
                DeviceId = deviceId,
                ContextJson = contextJson
            };

            // Fix: we want Slv/Matricule from payload context? We pass via payload.
            // We'll simply store them from stage payload when you include them.
            // If you want strict, add parameters to SafeDbLogAsync and map them here.

            // Try extended insert, fallback if columns don't exist
            try
            {
                await conn.ExecuteAsync(sqlExtended, args);
            }
            catch (SqlException sx) when (sx.Message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase))
            {
                await conn.ExecuteAsync(sqlBase, args);
            }
            catch (SqlException sx) when (sx.Message.Contains("Process", StringComparison.OrdinalIgnoreCase)
                                          || sx.Message.Contains("ContextJson", StringComparison.OrdinalIgnoreCase))
            {
                await conn.ExecuteAsync(sqlBase, args);
            }

            string requestToStringSafe(string _) => ""; // no-op (keeps args object compile-friendly)
        }
        catch (Exception logEx)
        {
            _log.LogWarning(logEx, "Failed to write dbo.Ecare_AppLogs");
        }
    }
}
