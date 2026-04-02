// File: PabExitListener.cs
using Ecare.Application.Services.Handlers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecare.Application.Services
{
    public sealed class LoadingListner : BackgroundService
    {
        private readonly SignalRHubListener _inner;

        public LoadingListner(
            ILogger<SignalRHubListener> log,
            IHttpClientFactory http,
            IOptionsMonitor<SignalRListenerOptions> options,
            LoadingInboundHandler handler)
        {
            var opts = options.Get("Loading");
            log.LogInformation("LoadingListener constructor: Hub={Hub}, Method={Method}",
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