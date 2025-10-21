using Ecare.Api.Options;
using Ecare.Application.Queries;            // ScanBySlvVm, OrderDto, OrderItemDto
using Ecare.Domain.Interfaces;             // ISlvScanPublisher
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;  // ServiceManager, ServiceHubContext
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using Ecare.Domain.Dtos;


namespace Ecare.Infrastructure.Services.SignalR;

public sealed class SlvScanPublisher : ISlvScanPublisher
{
    private readonly ServiceManager _manager;
    private readonly ConcurrentDictionary<string, ServiceHubContext> _contexts;
    private readonly ILogger<SlvScanPublisher> _log;
    private readonly ScanEventsOptions _opt;

    public SlvScanPublisher(
        ServiceManager manager,
        ConcurrentDictionary<string, ServiceHubContext> contexts,
        IOptions<ScanEventsOptions> opt,
        ILogger<SlvScanPublisher> log)
    {
        _manager = manager;
        _contexts = contexts;
        _opt = opt.Value;
        _log = log;
    }

    public async Task PublishAsync(SlvDtos.ScanBySlvVm vm, CancellationToken ct = default)
    {
        // Get (or create & cache) the hub context for the scan hub
        var hub = _opt.HubName;
        if (!_contexts.TryGetValue(hub, out var ctx))
        {
            ctx = await _manager.CreateHubContextAsync(hub, ct);
            _contexts[hub] = ctx;
        }

        // Build the payload exactly as you requested
        var payload = new
        {
            @event = "slvScanned",
            site = _opt.Site,
            kiosk = _opt.Kiosk,
            slv = vm.CarteSLV,                         // include SLV in your VM
            ts = DateTime.UtcNow.ToString("O"),
            driver = new
            {
                id = vm.DriverId,
                              // add in VM if not present
                plate = vm.Plate
            },
            client = new
            {
                name = vm.ClientName,
                sapOk = vm.SapOk                 // if you truly don’t want sapOk, remove this line
            },
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

        // Send to all clients connected to the scan hub
        try
        {
            await ctx.Clients.All.SendAsync(_opt.MethodName, payload, ct);
            _log.LogInformation("Published slvScanned for SLV={slv} to hub={hub} method={method}",
                vm.CarteSLV, hub, _opt.MethodName);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to publish slvScanned for SLV={slv}", vm.CarteSLV);
        }
    }
}
