// Ecare.Application/Services/PabEntryInboundHandler.cs
using Ecare.Application.Queries;
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
public sealed class ParkingOutboundOptions
{
    public string Hub { get; set; } = "pabentry_data_hub";
    public string Method { get; set; } = "PabEntryDataEvent";
}

public sealed class ParkingSlvInboundHandler : ISignalRInboundHandler
{
    private readonly ILogger<ParkingSlvInboundHandler> _log;
    private readonly IServiceProvider _sp;
    private readonly ServiceManager _signalR;
    private readonly ParkingOutboundOptions _outOpt;

    public ParkingSlvInboundHandler(
        ILogger<ParkingSlvInboundHandler> log,
        IServiceProvider sp,
        ServiceManager signalR,
        IOptions<ParkingOutboundOptions> outOpt)
    {
        _log = log;
        _sp = sp;
        _signalR = signalR;
        _outOpt = outOpt.Value;
    }

    public async Task HandleAsync(object payload, CancellationToken ct)
    {
        string? slv = TryExtractCarteSlv(payload);
        if (string.IsNullOrWhiteSpace(slv))
        {
            _log.LogWarning("ParkingInboundHandler: payload missing carteSlv. Raw={raw}",
                payload is JsonElement je ? JsonSerializer.Serialize(je) : payload.ToString());
            return;
        }

        _log.LogInformation("ParkingInboundHandler: Processing SLV={slv}", slv);

        using var scope = _sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // run your scan/query for PAB entry
        var result = await mediator.Send(new ScanBySlvQuery(slv), ct);
        if (!result.Success || result.Value is null)
        {
            _log.LogWarning("ParkingInboundHandler: Scan failed for SLV={slv}. Err={err}", slv, result.Error);
            return;
        }

        
    }

    private static string? TryExtractCarteSlv(object payload)
    {
        // JSON element (from SignalR) path
        if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("carteSlv", out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
            if (el.TryGetProperty("slv", out var v2) && v2.ValueKind == JsonValueKind.String)
                return v2.GetString();
        }

        // POCO path
        var p = payload.GetType().GetProperty("carteSlv")
                 ?? payload.GetType().GetProperty("slv");
        return p?.GetValue(payload)?.ToString();
    }
}
