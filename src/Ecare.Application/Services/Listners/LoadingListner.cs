// Ecare.Application/Services/Listners/LoadingListner.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Azure.SignalR.Management;

namespace Ecare.Application.Services
{
    public sealed class LoadingListner : BackgroundService
    {
        private readonly SignalRHubListener _inner;

        public LoadingListner(
            ILogger<SignalRHubListener> log,
            IHttpClientFactory http,
            IOptionsMonitor<SignalRListenerOptions> options,
            LoadingInboundHandler handler,
            ServiceManager manager) // <-- inject
        {
            var opts = options.Get("Loading");
            log.LogInformation("LoadingListener: Hub={Hub}, Method={Method}", opts.Hub, opts.Method);
            _inner = new SignalRHubListener(log, http, Options.Create(opts), handler, manager); // <-- pass
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
            => ((IHostedService)_inner).StartAsync(stoppingToken);

        public override Task StopAsync(CancellationToken cancellationToken)
            => _inner.StopAsync(cancellationToken);
    }
}
