// File: PabExitListener.cs
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecare.Application.Services
{
    public sealed class ParkingSlvListner : BackgroundService
    {
        private readonly SignalRHubListener _inner;

        public ParkingSlvListner(
            ILogger<SignalRHubListener> log,
            IHttpClientFactory http,
            IOptionsMonitor<SignalRListenerOptions> options,
            ParkingSlvInboundHandler handler,
            ServiceManager manager)
        {
            var opts = options.Get("Parking");
            log.LogInformation("ParkingSlvListner: Hub={Hub}, Method={Method}", opts.Hub, opts.Method);
            _inner = new SignalRHubListener(log, http, Options.Create(opts), handler, manager);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return ((IHostedService)_inner).StartAsync(stoppingToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return _inner.StopAsync(cancellationToken);
        }
    }
}