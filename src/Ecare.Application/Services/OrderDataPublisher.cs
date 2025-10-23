using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Management;
using Microsoft.AspNetCore.SignalR;

namespace Ecare.Application.Services
{
    public sealed class OrderDataPublisher : IAsyncDisposable
    {
        private readonly ILogger<OrderDataPublisher> _log;
        private readonly ServiceManager _manager;
        private readonly string _hubName = "order_data_hub";
        private ServiceHubContext? _hub;

        public OrderDataPublisher(ILogger<OrderDataPublisher> log, ServiceManager manager)
        {
            _log = log;
            _manager = manager;
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_hub is not null) return;

            _hub = await _manager.CreateHubContextAsync(_hubName, ct);
            _log.LogInformation("OrderDataPublisher connected to Azure SignalR hub '{hub}'", _hubName);
        }

        public async Task BroadcastOrderDataAsync(object payload)
        {
            if (_hub is null)
                throw new InvalidOperationException("Hub context not initialized");

            await _hub.Clients.All.SendAsync("OrderDataEvent", payload);
            _log.LogInformation("Broadcasted order_data_hub event: {json}", System.Text.Json.JsonSerializer.Serialize(payload));
        }

        public async ValueTask DisposeAsync()
        {
            if (_hub is not null)
                await _hub.DisposeAsync();
        }
    }
}
