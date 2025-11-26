using Ecare.Application.Queries.MobileQueries.GetFluxChargingDetails;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Services.Handlers
{
    public sealed class FluxRealtimeTicker : BackgroundService
    {
        private readonly ILogger<FluxRealtimeTicker> _log;
        private readonly ServiceManager _signalR; // same type you already use for hubs
        private readonly IServiceProvider _sp;

        public FluxRealtimeTicker(
            ILogger<FluxRealtimeTicker> log,
            ServiceManager signalR,
            IServiceProvider sp)
        {
            _log = log;
            _signalR = signalR;
            _sp = sp;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("Flux real-time ticker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    var result = await mediator.Send(new GetFluxChargingDetailsQuerie(), stoppingToken);
                    if (!result.Success)
                    {
                        _log.LogWarning("No flux data to broadcast: {error}", result.Error);
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                        continue;
                    }

                    var payload = result.Value!;

                    // Use your generic helper here:
                    await SignalRHelper.BroadcastAsync(
                        _signalR,
                        hubName: "dashboard_data_hub",
                        methodName: "DashboardDataEvent",
                        payload: payload,
                        logger: _log,
                        ct: stoppingToken
                    );

                    _log.LogInformation("Flux stages broadcasted at {time}", DateTime.Now);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Error while broadcasting flux stages");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // realtime refresh interval
            }
        }
    }
}
