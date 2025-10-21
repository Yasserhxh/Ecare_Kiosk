using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Ecare.Domain.Interfaces;          // ISignalRNegotiator
using Ecare.Domain.ValueObjects;       // NegotiateRequest, NegotiateResult (adjust namespace if different)
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Options;

namespace Ecare.Infrastructure.Services.AzureSignalR
{
    internal sealed class AzureSignalRNegotiator : ISignalRNegotiator
    {
        private readonly ServiceManager _manager;
        private readonly ConcurrentDictionary<string, ServiceHubContext> _contexts;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly SignalRInfraOptions _opt;

        public AzureSignalRNegotiator(
            ServiceManager manager,
            ConcurrentDictionary<string, ServiceHubContext> contexts,
            IOptions<SignalRInfraOptions> opt)
        {
            _manager = manager;
            _contexts = contexts;
            _opt = opt.Value;
        }

        public async Task<NegotiateResult> NegotiateAsync(NegotiateRequest request, CancellationToken ct = default)
        {
            var hub = string.IsNullOrWhiteSpace(request.HubName) ? _opt.DefaultHub : request.HubName!;

            // Fast path from cache
            if (!_contexts.TryGetValue(hub, out var ctx))
            {
                // Ensure only one creator per hub
                var gate = _locks.GetOrAdd(hub, static _ => new SemaphoreSlim(1, 1));
                await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (!_contexts.TryGetValue(hub, out ctx))
                    {
                        // NOTE: If your package version requires, swap ct with CancellationToken.None
                        ctx = await _manager.CreateHubContextAsync(hub, ct).ConfigureAwait(false);
                        _contexts[hub] = ctx;
                    }
                }
                finally
                {
                    gate.Release();
                }
            }

            // Anonymous negotiation (no user claims)
            var info = await ctx.NegotiateAsync().ConfigureAwait(false);

            return new NegotiateResult
            {
                Url = info.Url,
                AccessToken = info.AccessToken,
                Hub = hub
            };
        }
    }
}
