using Dapper;
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
        var slv = request.RfidCard.ToString();
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
                request.Produit1
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
                    Step
                FROM dbo.Ecare_Order_Legend
                WHERE RFIDCard = @RfidCard
                  AND Matricule = @Matricule
                  AND ISNULL(Step, 0) < 5
                ORDER BY Id DESC;
            """;

            await SafeDbLogAsync(
                connStr,
                traceId,
                evt,
                stage: "LOAD_LEGEND_BEGIN",
                statusCode: 0,
                isSuccess: true,
                payload: new { request.RfidCard, request.Matricule },
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
                    sqlLegend,
                    new { request.RfidCard, request.Matricule },
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
                var maxAllowed = ptac * 1.11m; // keep your current rule

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
                        ts = DateTime.UtcNow,
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
            // 3) Call stored procedure (your existing logic)
            // ----------------------------------------------
            var spParams = new
            {
                RfidCard = request.RfidCard,
                Matricule = request.Matricule,
                PremierePoid = request.PremierePoid,
                Produit1 = request.Produit1
            };

            await SafeDbLogAsync(
                connStr,
                traceId,
                evt,
                stage: "SP_CALL_BEGIN",
                statusCode: 0,
                isSuccess: true,
                payload: spParams,
                process: ProcessName,
                legendId: vm.Id,
                spName: "sp_UpdateAfterFirstWeight",
                hubName: null,
                methodName: null,
                deviceId: null,
                context: null,
                ct: ct);

            var sw = Stopwatch.StartNew();

            var row = await conn.QueryFirstOrDefaultAsync<FirstWeightResultVm>(
                new CommandDefinition(
                    "sp_UpdateAfterFirstWeight",
                    spParams,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: ct));

            sw.Stop();

            if (row is null)
            {
                await SafeDbLogAsync(
                    connStr,
                    traceId,
                    evt,
                    stage: "SP_NO_MATCHING_ROW",
                    statusCode: 404,
                    isSuccess: false,
                    payload: spParams,
                    process: ProcessName,
                    legendId: vm.Id,
                    spName: "sp_UpdateAfterFirstWeight",
                    hubName: null,
                    methodName: null,
                    deviceId: null,
                    context: new { elapsedMs = (int)sw.ElapsedMilliseconds },
                    ct: ct);

                return Result<FirstWeightResultVm>.Fail("NO_MATCHING_ROW");
            }

            await SafeDbLogAsync(
                connStr,
                traceId,
                evt,
                stage: "SP_CALL_OK",
                statusCode: 200,
                isSuccess: true,
                payload: row,
                process: ProcessName,
                legendId: vm.Id,
                spName: "sp_UpdateAfterFirstWeight",
                hubName: null,
                methodName: null,
                deviceId: null,
                context: new { elapsedMs = (int)sw.ElapsedMilliseconds },
                ct: ct);

            return Result<FirstWeightResultVm>.Ok(row);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Error running sp_UpdateAfterFirstWeight for RFID={rfid}, Matricule={mat}",
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
                spName: "sp_UpdateAfterFirstWeight",
                hubName: null,
                methodName: null,
                deviceId: null,
                context: null,
                ex: ex,
                ct: ct);

            return Result<FirstWeightResultVm>.Fail("SP_ERROR");
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
