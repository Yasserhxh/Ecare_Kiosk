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
        string? slv = TryExtractCarteSlv(payload);
        if (string.IsNullOrWhiteSpace(slv))
        {
            _log.LogWarning("LoadingInboundHandler: payload missing carteSlv. Raw={raw}",
                payload is JsonElement je ? JsonSerializer.Serialize(je) : payload.ToString());
            return;
        }

        _log.LogInformation("LoadingInboundHandler: Processing SLV={slv}", slv);

        using var scope = _sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // run your scan/query for PAB entry
        var result = await mediator.Send(new GetOrderDetailsQuery(slv), ct);
        if (!result.Success || result.Value is null)
        {
            _log.LogWarning("LoadingInboundHandler: Scan failed for SLV={slv}. Err={err}", slv, result.Error);
            return;
        }

        var vm = result.Value;

        // shape payload exactly like your other broadcasts
        var enriched = new
        {
            @event = _outOpt.Method,             
            site = "Asment-Temara-01",
            kiosk = "loading-pc-01",
            slv = vm.CarteSLV,
            ts = DateTime.UtcNow,
            driver = new { id = vm.DriverId, plate = vm.Plate },
            client = new { name = vm.ClientName, sapOk = vm.SapOk },
            order = vm.Order is null ? null : new
            {
                number = vm.Order.Number,
                destination = vm.Order.Destination,
                deliveryMode = vm.Order.DeliveryMode,
                truckPlate = vm.Order.TruckPlate,
                status = vm.Order.Status,
                items = vm.Order.Items?.Select(i => new
                {
                    productId = i.ProductId,
                    productName = i.ProductName,
                    quantity = i.Quantity,
                    unite = i.Unite
                })
            }
        };

        // broadcast to Azure SignalR
        await SignalRHelper.BroadcastAsync(
            _signalR,
            hubName: _outOpt.Hub,        
            methodName: _outOpt.Method,  
            payload: enriched,
            logger: _log,
            ct: ct
        );

        _log.LogInformation("LoadingInboundHandler: Broadcasted {method} to {hub} for SLV={slv}",
            _outOpt.Method, _outOpt.Hub, slv);
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
