// File: PabEntryListener.cs
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecare.Application.Services.Listners
{
    public sealed class PabEntryListener : BackgroundService
    {
        private readonly SignalRHubListener _inner;

        public PabEntryListener(
        ILogger<SignalRHubListener> log,
        IHttpClientFactory http,
        IOptionsMonitor<SignalRListenerOptions> options,
        PabEntryInboundHandler handler,
        ServiceManager manager)
        {
            var opts = options.Get("PabEntry");
            log.LogInformation("PabEntryListener: Hub={Hub}, Method={Method}", opts.Hub, opts.Method);
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