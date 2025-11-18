using Dapper;
using Ecare.Application.Queries;
using Ecare.Application.Queries.GetLoadingData;
using Ecare.Application.Services.Ecare.Application.Services;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Ecare.Application.Services;

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
    private readonly LoadingOutboundOptions _opt;
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
        _opt = outOpt.Value;

        _connString = cfg.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("ConnectionStrings:SqlServer missing");
    }

    // ====================================================================================
    // ULTRA FAST MODE — Only 3 conditions:
    //    1) SLV validation
    //    2) deviceId validation
    //    3) Line matching (device line vs Flux.Ligne)
    // ====================================================================================
    public async Task HandleAsync(object payload, CancellationToken ct)
    {
        // 1) SLV validation
        string? slv = TryExtractCarteSlv(payload);
        if (string.IsNullOrWhiteSpace(slv))
        {
            _log.LogWarning("LoadingInboundHandler: missing SLV. Raw={raw}",
                payload is JsonElement je ? JsonSerializer.Serialize(je) : payload.ToString());
            return;
        }

        // 2) Device ID validation
        string? deviceId = TryExtractDeviceId(payload);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            _log.LogWarning("LoadingInboundHandler: missing deviceId for SLV={slv}", slv);
            return;
        }

        _log.LogInformation("FAST LOADING: SLV={slv} device={dev}", slv, deviceId);

        using var scope = _sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // One DB call only — stored procedure + mapper
        var result = await mediator.Send(new GetLoadingDataQuery(slv), ct);
        if (!result.Success || result.Value is null)
        {
            _log.LogWarning("LoadingInboundHandler: no data for SLV={slv}", slv);
            return;
        }

        var vm = result.Value;

        // ====================================================================================
        // 3) Get line of current device & expected line from last EcareFlux row
        // ====================================================================================

        string? scannedLine;
        string? expectedLine;

        await using (var conn = new SqlConnection(_connString))
        {
            await conn.OpenAsync(ct);

            // ---- A) Scanned line from Ecare_DeviceAssignment
            const string sqlScanned = """
                SELECT TOP(1) l.Nom
                FROM dbo.Ecare_DeviceAssignment da
                JOIN dbo.Ecare_Ligne l ON l.Id = da.LineId
                WHERE da.DeviceId = @dev AND da.EffectiveTo IS NULL;
            """;

            scannedLine = await conn.ExecuteScalarAsync<string?>(
                sqlScanned, new { dev = deviceId });

            // ---- B) Expected line from last EcareFlux row (only Ligne)
            const string sqlExpected = """
                SELECT TOP(1) Ligne
                FROM dbo.EcareFlux
                WHERE CarteSlv = @slv
                ORDER BY Id DESC;
            """;

            expectedLine = await conn.ExecuteScalarAsync<string?>(
                sqlExpected, new { slv });
        }

        // Normalize both
        string norm(string? s) =>
            string.IsNullOrWhiteSpace(s)
                ? ""
                : s.Trim().ToLowerInvariant();

        bool linesMatch = norm(scannedLine) == norm(expectedLine);

        // ====================================================================================
        // Build payload (minimal)
        // ====================================================================================
        var basePayload = new
        {
            @event = "LoadingDataEvent",
            site = "Asment-Temara-01",
            kiosk = deviceId,
            slv = vm.CarteSLV,
            ts = DateTime.UtcNow,
            driver = new
            {
                name = vm.DriverName,
                plate = vm.Plate
            },
            client = new
            {
                name = vm.ClientName,
                sapOk = vm.SapOk
            },
            firstWeight = vm.FirstWeight,
            pabEntryAt = vm.PabEntryAt,
            order = vm.Order
        };

        if (linesMatch)
        {
            await SignalRHelper.BroadcastToDeviceAsync(
                _signalR, _opt.Hub, _opt.Method, deviceId, basePayload, _log, ct);

            _log.LogInformation("FAST LOADING: OK line='{line}'", scannedLine);
            return;
        }

        // WRONG LINE PAYLOAD
        var wrongPayload = new
        {
            @event = "LoadingDataEvent",
            ok = false,
            site = "Asment-Temara-01",
            kiosk = deviceId,
            slv = vm.CarteSLV,
            ts = DateTime.UtcNow,
            message = expectedLine is null
                ? "Ligne attendue introuvable. Veuillez contacter l'opérateur."
                : $"Vous n’êtes pas dans votre ligne. Dirigez-vous vers « {expectedLine} ».",

            expectedLine,
            scannedLine,

            driver = new { name = vm.DriverName, plate = vm.Plate },
            client = new { name = vm.ClientName, sapOk = vm.SapOk },
            order = vm.Order
        };

        await SignalRHelper.BroadcastToDeviceAsync(
            _signalR, _opt.Hub, _opt.Method, deviceId, wrongPayload, _log, ct);

        _log.LogWarning("FAST LOADING: WRONG LINE scanned='{s}' expected='{e}'",
            scannedLine, expectedLine);
    }

    // ====================================================================================
    //  Helpers
    // ====================================================================================
    private static string? TryExtractCarteSlv(object payload)
    {
        if (payload is JsonElement el)
        {
            if (el.TryGetProperty("carteSlv", out var v1) && v1.ValueKind == JsonValueKind.String)
                return v1.GetString();
            if (el.TryGetProperty("slv", out var v2) && v2.ValueKind == JsonValueKind.String)
                return v2.GetString();
        }

        return payload.GetType().GetProperty("carteSlv")
            ?.GetValue(payload)?.ToString()
            ?? payload.GetType().GetProperty("slv")
            ?.GetValue(payload)?.ToString();
    }

    private static string? TryExtractDeviceId(object payload)
    {
        if (payload is JsonElement el &&
            el.TryGetProperty("deviceId", out var v) &&
            v.ValueKind == JsonValueKind.String)
            return v.GetString();

        return payload.GetType().GetProperty("deviceId")
            ?.GetValue(payload)?.ToString();
    }
}
