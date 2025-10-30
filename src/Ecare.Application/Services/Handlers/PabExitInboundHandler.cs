// PabExitInboundHandler.cs
using Ecare.Application.Queries;
using Ecare.Application.Services;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

public sealed class PabExitOutboundOptions
{
    public string Hub { get; set; } = "pabexit_data_hub";
    public string Method { get; set; } = "PabExitDataEvent";
}

public sealed class PabExitInboundHandler : ISignalRInboundHandler
{
    private readonly ILogger<PabExitInboundHandler> _log;
    private readonly IServiceProvider _sp;
    private readonly ServiceManager _signalR;
    private readonly PabExitOutboundOptions _outOpt;

    public PabExitInboundHandler(
        ILogger<PabExitInboundHandler> log,
        IServiceProvider sp,
        ServiceManager signalR,
        IOptions<PabExitOutboundOptions> outOpt)
    {
        _log = log; _sp = sp; _signalR = signalR; _outOpt = outOpt.Value;
    }

    public async Task HandleAsync(object payload, CancellationToken ct)
    {
        var slv = TryExtract(payload, "carteSlv") ?? TryExtract(payload, "slv");
        if (string.IsNullOrWhiteSpace(slv))
        {
            _log.LogWarning("PabExit: payload missing carteSlv/slv. Raw={raw}",
                payload is JsonElement je ? JsonSerializer.Serialize(je) : payload?.ToString());
            return;
        }

        var deviceId = TryExtract(payload, "deviceId");
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            _log.LogWarning("PabExit: payload missing deviceId for SLV={slv}", slv);
            return;
        }

        using var scope = _sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new GetPabExitDataQuery(slv), ct);
        if (!result.Success || result.Value is null)
        {
            _log.LogWarning("PabExit: Scan failed for SLV={slv}. Err={err}", slv, result.Error);
            return;
        }

        var vm = result.Value;
        var enriched = new
        {
            @event = _outOpt.Method,
            site = "Asment-Temara-01",
            kiosk = "pab-exit-pc-01",
            slv = vm.CarteSLV,
            firstWeight = vm.FirstWeight,
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

        await SignalRHelper.BroadcastToDeviceAsync(
            _signalR, _outOpt.Hub, _outOpt.Method, deviceId, enriched, _log, ct);

        _log.LogInformation("PabExit: sent {method} to {hub} for device={deviceId}, SLV={slv}",
            _outOpt.Method, _outOpt.Hub, deviceId, slv);
    }

    private static string? TryExtract(object payload, string name)
    {
        if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
            return el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var p = payload.GetType().GetProperty(name);
        return p?.GetValue(payload)?.ToString();
    }
}
