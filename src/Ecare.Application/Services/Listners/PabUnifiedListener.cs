using Ecare.Application.Services;
using Ecare.Application.Services.Handlers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecare.Application.Services
{
    public sealed class PabUnifiedListener : BackgroundService
    {
        private readonly SignalRHubListener _entryListener;
        private readonly SignalRHubListener _exitListener;
        private readonly ILogger<PabUnifiedListener> _log;

        public PabUnifiedListener(
            ILogger<SignalRHubListener> listenerLog,
            ILogger<PabUnifiedListener> log,
            IHttpClientFactory http,
            IOptionsMonitor<SignalRListenerOptions> options,
            PabUnifiedInboundHandler handler)
        {
            _log = log;

            var entryOpts = options.Get("PabEntry");
            _entryListener = new SignalRHubListener(
                listenerLog, http,
                Options.Create(entryOpts),
                new ForwardingHandler(handler, isExit: false));

            var exitOpts = options.Get("PabExit");
            _exitListener = new SignalRHubListener(
                listenerLog, http,
                Options.Create(exitOpts),
                new ForwardingHandler(handler, isExit: true));

            _log.LogInformation(
                "PabUnifiedListener: Entry={eHub} | Exit={xHub}",
                entryOpts.Hub, exitOpts.Hub);
        }

        protected override Task ExecuteAsync(CancellationToken ct) =>
            Task.WhenAll(
                ((IHostedService)_entryListener).StartAsync(ct),
                ((IHostedService)_exitListener).StartAsync(ct));

        public override async Task StopAsync(CancellationToken ct)
        {
            await Task.WhenAll(_entryListener.StopAsync(ct), _exitListener.StopAsync(ct));
            await base.StopAsync(ct);
        }

        private sealed class ForwardingHandler : ISignalRInboundHandler
        {
            private readonly PabUnifiedInboundHandler _inner;
            private readonly bool _isExit;

            public ForwardingHandler(PabUnifiedInboundHandler inner, bool isExit)
            {
                _inner = inner;
                _isExit = isExit;
            }

            public Task HandleAsync(object payload, CancellationToken ct) =>
                _inner.HandleAsync(payload, ct);
        }
    }
}