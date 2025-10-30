// Ecare.Application/Services/PabEntryInboundHandler.cs
using Ecare.Application.Queries;
using Ecare.Application.Queries.Loading.GetOrderDetails;
using Ecare.Application.Services.Ecare.Application.Services;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Ecare.Application.Services;

// Outbound (broadcast) options you can bind from appsettings:
// "PabEntryOutbound": { "Hub": "pabentry_data_hub", "Method": "PabEntryDataEvent" }
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

    public LoadingInboundHandler(
        ILogger<LoadingInboundHandler> log,
        IServiceProvider sp,
        ServiceManager signalR,
        IOptions<LoadingOutboundOptions> outOpt)
    {
        _log = log;
        _sp = sp;
        _signalR = signalR;
        _outOpt = outOpt.Value;
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

        var vm = result.Value;

        var outboundPayload = new
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

        // Broadcast to specific device
        await SignalRHelper.BroadcastToDeviceAsync(
            _signalR,
            hubName: _outOpt.Hub,
            methodName: _outOpt.Method,
            deviceId: deviceId,
            payload: outboundPayload,
            logger: _log,
            ct: ct
        );

        _log.LogInformation("✅ Loading: Sent to device={device}", deviceId);
    }

    private static string? TryExtractCarteSlv(object payload)
    {
        if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("carteSlv", out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
            if (el.TryGetProperty("slv", out var v2) && v2.ValueKind == JsonValueKind.String)
                return v2.GetString();
        }

        var p = payload.GetType().GetProperty("carteSlv")
                 ?? payload.GetType().GetProperty("slv");
        return p?.GetValue(payload)?.ToString();
    }

    private static string? TryExtractDeviceId(object payload)
    {
        if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("deviceId", out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }

        var p = payload.GetType().GetProperty("deviceId");
        return p?.GetValue(payload)?.ToString();
    }
}

