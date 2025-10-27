// File: PabExitListener.cs
using Ecare.Application.Services.Ecare.Application.Services;
using Ecare.Application.Services.Handlers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecare.Application.Services
{
    public sealed class PabExitListener : BackgroundService
    {
        private readonly SignalRHubListener _inner;

        public PabExitListener(
            ILogger<SignalRHubListener> log,
            IHttpClientFactory http,
            IOptionsMonitor<SignalRListenerOptions> options,
            PabExitInboundHandler handler)
        {
            var opts = options.Get("PabExit");
            log.LogInformation("🟠 PabExitListener constructor: Hub={Hub}, Method={Method}",
                opts.Hub, opts.Method);
            _inner = new SignalRHubListener(log, http, Options.Create(opts), handler);
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