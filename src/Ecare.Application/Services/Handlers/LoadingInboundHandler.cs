using Dapper;
using Ecare.Application.Queries;
using Ecare.Application.Services.Ecare.Application.Services;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;
using System.Text.Json;

namespace Ecare.Application.Services;

// Outbound (broadcast) options you can bind from appsettings:
// "LoadingOutbound": { "Hub": "loading_data_hub", "Method": "LoadingDataEvent" }
public sealed class LoadingOutboundOptions
{
    public string Hub { get; set; } = "loading_data_hub";
    public string Method { get; set; } = "LoadingDataEvent";
}

public sealed class LoadingInboundHandler : ISignalRInboundHandler
{
    private readonly ILogger<LoadingInboundHandler> _log;
    private readonly IServiceProvider _sp;
    private readonly ServiceManager _signalR;
    private readonly LoadingOutboundOptions _outOpt;
    private readonly string _connString;

    public LoadingInboundHandler(
        ILogger<LoadingInboundHandler> log,
        IServiceProvider sp,
        ServiceManager signalR,
        IOptions<LoadingOutboundOptions> outOpt,
        IConfiguration cfg)
    {
        _log = log;
        _sp = sp;
        _signalR = signalR;
        _outOpt = outOpt.Value;
        _connString = cfg.GetConnectionString("SqlServer")
                     ?? throw new InvalidOperationException("ConnectionStrings:SqlServer missing");
    }

    public async Task HandleAsync(object payload, CancellationToken ct)
    {
        // Extract SLV
        string? slv = TryExtractCarteSlv(payload);
        if (string.IsNullOrWhiteSpace(slv))
        {
            _log.LogWarning("LoadingInboundHandler: payload missing carteSlv. Raw={raw}",
                payload is JsonElement je ? JsonSerializer.Serialize(je) : payload.ToString());
            return;
        }

        // Extract deviceId
        string? deviceId = TryExtractDeviceId(payload);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            _log.LogWarning("LoadingInboundHandler: Missing deviceId for SLV={slv}", slv);
            return;
        }

        _log.LogInformation("Loading: Processing SLV={slv} from device={device}", slv, deviceId);

        using var scope = _sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new ScanBySlvQuery(slv), ct);
        if (!result.Success || result.Value is null)
        {
            _log.LogWarning("Loading: Scan failed for SLV={slv}. Err={err}", slv, result.Error);
            return;
        }
        // Check EcareFlux (latest valid row for this SLV)
        var flux = await GetLatestValidFluxAsync(scope, slv, ct);
        if (flux is null || flux.FirstWeight is null)
        {
            // No matching flux row OR invalid row -> do not send anything.
            _log.LogInformation(
                "PabEntry: No valid EcareFlux row for SLV={slv} (no row, or Status <> 1, or FirstWeight NULL)", slv);

            return;
        }
        var vm = result.Value; // must include: CarteSLV, DriverId, DriverName, Plate, ClientName, SapOk, Order (optional)

        // -------------------------
        // Line check (device vs expected)
        // -------------------------
        (int? lineId, string? lineName) currLine;
        (int? lineId, string? lineName) expectedLine;

        await using var db = new SqlConnection(_connString);
        await db.OpenAsync(ct);

        currLine = await GetCurrentLineByDeviceAsync(db, deviceId!, ct);
        expectedLine = await GetExpectedLineForTruckAsync(db, vm.Plate, vm.Order?.Number, ct);

        bool linesMatch =
            !string.IsNullOrWhiteSpace(currLine.lineName) &&
            !string.IsNullOrWhiteSpace(expectedLine.lineName) &&
            string.Equals(currLine.lineName!.Trim(), expectedLine.lineName!.Trim(), StringComparison.OrdinalIgnoreCase);

        // Base payload (OK case)
        var okPayload = new
        {
            @event = "LoadingDataEvent",
            site = "Asment-Temara-01",
            kiosk = deviceId,
            slv = vm.CarteSLV,
            ts = DateTime.UtcNow,
            driver = new
            {
                id = vm.DriverId,
                name = vm.DriverName,
                plate = vm.Plate
            },
            client = new
            {
                name = vm.ClientName,
                sapOk = vm.SapOk
            },
            order = vm.Order
        };

        if (linesMatch)
        {
            // Correct line → send as-is
            await SignalRHelper.BroadcastToDeviceAsync(
                _signalR, _outOpt.Hub, _outOpt.Method, deviceId!, okPayload, _log, ct);

            _log.LogInformation("Loading: OK (line '{line}') for device={device}", currLine.lineName, deviceId);
            return;
        }

        // Wrong line → guidance payload
        var wrongPayload = new
        {
            @event = "LoadingDataEvent",
            ok = false,
            site = "Asment-Temara-01",
            kiosk = deviceId,
            slv = vm.CarteSLV,
            ts = DateTime.UtcNow,
            message = expectedLine.lineName is null
                ? "Ligne attendue introuvable. Veuillez contacter l'opérateur."
                : $"Vous n’êtes pas dans votre ligne. Dirigez-vous vers « {expectedLine.lineName} ».",
            expectedLine = expectedLine.lineName,
            scannedLine = currLine.lineName,
            driver = new { id = vm.DriverId, name = vm.DriverName, plate = vm.Plate },
            client = new { name = vm.ClientName, sapOk = vm.SapOk },
            order = vm.Order
        };

        await SignalRHelper.BroadcastToDeviceAsync(
            _signalR, _outOpt.Hub, _outOpt.Method, deviceId!, wrongPayload, _log, ct);

        _log.LogWarning("Loading: WRONG line. scanned='{scanned}', expected='{expected}', device={device}",
            currLine.lineName ?? "?", expectedLine.lineName ?? "?", deviceId);
    }

    // ----------------- Helpers -----------------

    private static string? TryExtractCarteSlv(object payload)
    {
        if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("carteSlv", out var v) && v.ValueKind == JsonValueKind.String) return v.GetString();
            if (el.TryGetProperty("slv", out var v2) && v2.ValueKind == JsonValueKind.String) return v2.GetString();
        }

        var p = payload.GetType().GetProperty("carteSlv") ?? payload.GetType().GetProperty("slv");
        return p?.GetValue(payload)?.ToString();
    }

    private static string? TryExtractDeviceId(object payload)
    {
        if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("deviceId", out var v) && v.ValueKind == JsonValueKind.String) return v.GetString();
        }
        var p = payload.GetType().GetProperty("deviceId");
        return p?.GetValue(payload)?.ToString();
    }

    private static async Task<(int? lineId, string? lineName)> GetCurrentLineByDeviceAsync(
        IDbConnection db, string deviceId, CancellationToken ct)
    {
        const string sql = """
        SELECT TOP(1) l.Id AS LineId, l.Nom AS LineName
        FROM dbo.Ecare_DeviceAssignment da
        JOIN dbo.Ecare_Ligne l ON l.Id = da.LineId
        WHERE da.DeviceId = @DeviceId AND da.EffectiveTo IS NULL;
        """;

        var row = await db.QuerySingleOrDefaultAsync(sql, new { DeviceId = deviceId });
        return row is null
            ? (null, null)
            : ((int?)row.LineId, (string?)row.LineName);
    }

    private static async Task<(int? lineId, string? lineName)> GetExpectedLineForTruckAsync(
        IDbConnection db, string plate, string? orderNumber, CancellationToken ct)
    {
        // 1) Preferred: Queue assignment
        const string q1 = """
        ;WITH Flux AS
        (
            SELECT
                f.*,
                CASE
                    WHEN f.StartChargingAt IS NOT NULL AND f.FinishedChargingAt IS NULL THEN 'Loading'
                    WHEN (f.Ligne IS NOT NULL OR f.PabEntryAt IS NOT NULL) AND f.StartChargingAt IS NULL THEN 'Called'
                    ELSE 'Waiting'
                END AS Status,
                (SELECT MAX(v) FROM (VALUES (f.StartChargingAt), (f.PabEntryAt), (f.ParkedAt)) AS t(v)) AS LastEventAt
            FROM dbo.EcareFlux AS f
        )
        SELECT TOP (1)
            l.Id   AS LineId,
            l.Nom AS LineName
        FROM Flux AS q
        LEFT JOIN dbo.Ecare_Ligne AS l
            ON l.Nom = q.Ligne
        WHERE q.Matricule = @Plate
          AND q.Status IN ('Waiting','Called','Loading')
        ORDER BY
            CASE q.Status WHEN 'Loading' THEN 0 WHEN 'Called' THEN 1 WHEN 'Waiting' THEN 2 ELSE 3 END,
            q.LastEventAt DESC;
        """;


        var rowQ = await db.QuerySingleOrDefaultAsync(q1, new { Plate = plate });
        if (rowQ is not null)
            return ((int?)rowQ.LineId, (string?)rowQ.LineName);

        // 2) Fallback: Flux (name stored in column 'Ligne')
        //    Join back to Ecare_Ligne by trimmed name to recover LineId where possible.
        const string q2 = """
        SELECT TOP(1)
            l.Id     AS LineId,
            f.Ligne  AS LineName
        FROM dbo.EcareFlux f
        LEFT JOIN dbo.Ecare_Ligne l
          ON LTRIM(RTRIM(l.Nom)) = LTRIM(RTRIM(f.Ligne))
        WHERE f.Matricule = @Plate
          AND (f.PabExitAt IS NULL OR f.SecondWeight IS NULL)
        ORDER BY f.Id DESC;
        """;

        var rowF = await db.QuerySingleOrDefaultAsync(q2, new { Plate = plate });
        return rowF is null
            ? (null, null)
            : ((int?)rowF.LineId, (string?)rowF.LineName);
    }

    private static async Task<FluxSnapshot?> GetLatestValidFluxAsync(
        IServiceScope scope,
        string carteSlv,
        CancellationToken ct)
    {
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var connStr = config.GetConnectionString("SqlServer");
        if (string.IsNullOrWhiteSpace(connStr))
            throw new InvalidOperationException("Missing 'SqlServer' connection string.");

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);

        const string sql = @"
         SELECT TOP(1)
             Ligne,
             TotalCharged,
             FirstWeight
         FROM dbo.EcareFlux
         WHERE 
             CarteSlv = @CarteSlv
              
            AND StartChargingAt IS NULL
         ORDER BY ParkedAt ;";

        return await conn.QueryFirstOrDefaultAsync<FluxSnapshot>(
            new CommandDefinition(
                sql,
                new { CarteSlv = carteSlv },
                cancellationToken: ct));
    }

    private sealed class FluxSnapshot
    {
        public decimal? FirstWeight { get; init; }
        public string Ligne { get; init; } = default!;
        public decimal? TotalCharged { get; init; }
    }
}
