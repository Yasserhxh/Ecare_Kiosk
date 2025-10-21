 
using Ecare.Domain.Interfaces;
using Ecare.Infrastructure.Services.SignalR; // if your options live here
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using static Ecare.Domain.Dtos.SlvDtos;

namespace Ecare.Infrastructure.Services.SignalR
{
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

        // 👇 Signature MUST match interface exactly
        public async Task PublishAsync(ScanBySlvVm vm, CancellationToken ct = default)
        {
            var hub = _opt.HubName ?? "slv_scan_hub";
            if (!_contexts.TryGetValue(hub, out var ctx))
            {
                ctx = await _manager.CreateHubContextAsync(hub, ct);
                _contexts[hub] = ctx;
            }

            var payload = new
            {
                @event = "slvScanned",
                site = _opt.Site,
                kiosk = _opt.Kiosk,
                slv = vm.CarteSLV, // you’re using CarteSLV in your code
                ts = DateTime.UtcNow.ToString("O"),
                driver = new
                {
                    id = vm.DriverId, 
                    plate = vm.Plate
                },
                client = new
                {
                    name = vm.ClientName,
                    sapOk = vm.SapOk
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

    public sealed class ScanEventsOptions
    {
        public string HubName { get; set; } = "slv_scan_hub";
        public string MethodName { get; set; } = "ReceiveScan";
        public string Site { get; set; } = "Asment-Temara-01";
        public string Kiosk { get; set; } = "Parking";
    }
}
