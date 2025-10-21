using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Ecare.Api.Options;          // RfidListenerOptions
using Ecare.Application.Queries;  // ScanBySlvQuery
using Ecare.Domain.Interfaces;    // ISignalRNegotiator
using Ecare.Domain.ValueObjects;  // NegotiateRequest

namespace Ecare.Api.Services
{
    public sealed class RfidSignalRListener : IHostedService
    {
        private readonly ILogger<RfidSignalRListener> _log;
        private readonly ISignalRNegotiator _negotiator;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly RfidListenerOptions _opt;

        private HubConnection? _conn;
        private readonly SemaphoreSlim _connectLock = new(1, 1);
        private CancellationTokenSource? _runCts;
        private string? _fixedUrl; // set on first negotiate; reused afterwards

        public RfidSignalRListener(
            ILogger<RfidSignalRListener> log,
            ISignalRNegotiator negotiator,
            IServiceScopeFactory scopeFactory,
            IOptions<RfidListenerOptions> opt,
            IHostApplicationLifetime lifetime)
        {
            _log = log;
            _negotiator = negotiator;
            _scopeFactory = scopeFactory;
            _lifetime = lifetime;
            _opt = opt.Value;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Defer until app is fully started so Swagger/UI are ready first
            _lifetime.ApplicationStarted.Register(() =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(300, cancellationToken); // tiny delay
                        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        await EnsureConnectedAsync(_runCts.Token);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "RFID SignalR listener failed during initial connect.");
                    }
                }, cancellationToken);
            });

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try { _runCts?.Cancel(); } catch { }
            await _connectLock.WaitAsync(cancellationToken);
            try
            {
                if (_conn is not null)
                {
                    try { await _conn.StopAsync(cancellationToken); } catch { }
                    try { await _conn.DisposeAsync(); } catch { }
                    _conn = null;
                }
            }
            finally
            {
                _connectLock.Release();
            }
        }

        /// <summary>
        /// Ensures a single connection attempt at a time. No outer loops; relies on events.
        /// </summary>
        private async Task EnsureConnectedAsync(CancellationToken ct)
        {
            await _connectLock.WaitAsync(ct);
            try
            {
                if (_conn is not null && _conn.State is HubConnectionState.Connected or HubConnectionState.Connecting)
                    return;

                // First-time negotiate to get URL + token; URL will be fixed for this connection's lifetime
                var first = await _negotiator.NegotiateAsync(new NegotiateRequest { HubName = _opt.HubName }, ct);
                _fixedUrl ??= first.Url;

                // Build the connection with a token provider that re-negotiate on each (re)connect
                _conn = new HubConnectionBuilder()
                    .WithUrl(_fixedUrl, opts =>
                    {
                        opts.AccessTokenProvider = async () =>
                        {
                            var n = await _negotiator.NegotiateAsync(new NegotiateRequest { HubName = _opt.HubName }, ct);
                            if (!string.Equals(_fixedUrl, n.Url, StringComparison.OrdinalIgnoreCase))
                            {
                                // Azure SignalR client URL should be stable (hub-based). Log if it changes.
                                _log.LogWarning("Negotiate returned a different URL than the fixed one. Using fixed URL.\nFixed: {fixedUrl}\nNew:   {newUrl}", _fixedUrl, n.Url);
                            }
                            return n.AccessToken;
                        };
                    })
                    .WithAutomaticReconnect(new[]
                    {
                        TimeSpan.Zero, TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)
                    })
                    .Build();

                WireHandlers(_conn);

                await _conn.StartAsync(ct);
                _log.LogInformation("SignalR listener connected. Listening on '{method}'.", _opt.IncomingMethod);
            }
            finally
            {
                _connectLock.Release();
            }
        }

        private void WireHandlers(HubConnection conn)
        {
            // Incoming RFID payload → run ScanBySlvQuery in a DI scope
            conn.On<JsonElement>(_opt.IncomingMethod, async payload =>
            {
                try
                {
                    var slv = ExtractSlv(payload);
                    if (string.IsNullOrWhiteSpace(slv))
                    {
                        _log.LogWarning("RFID payload missing SLV: {json}", payload.ToString());
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    var result = await mediator.Send(new ScanBySlvQuery(slv), _runCts?.Token ?? CancellationToken.None);

                    // Adjust if your Result<T> has a different shape
                    if (!result.Success)
                    {
                        _log.LogWarning("ScanBySlv failed for {slv}: {err}", slv, result.Error);
                        return;
                    }

                    var vm = result.Value;
                    _log.LogInformation("Scan OK {slv} -> Driver={driver} Plate={plate} Client={client}",
                        slv, vm.DriverId, vm.Plate, vm.ClientName ?? "-");

                    // OPTIONAL: resolve a publisher from 'scope' and broadcast vm
                    // var publisher = scope.ServiceProvider.GetRequiredService<IScanResultPublisher>();
                    // await publisher.PublishScanResultAsync(vm, _runCts?.Token ?? CancellationToken.None);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Error handling RFID payload.");
                }
            });

            conn.Reconnecting += error =>
            {
                _log.LogWarning(error, "SignalR reconnecting…");
                return Task.CompletedTask;
            };

            conn.Reconnected += id =>
            {
                _log.LogInformation("SignalR reconnected. ConnId={id}", id);
                return Task.CompletedTask;
            };

            conn.Closed += async error =>
            {
                _log.LogWarning(error, "SignalR connection closed; attempting re-connect with fresh negotiate...");
                if (_runCts is { IsCancellationRequested: false })
                {
                    try
                    {
                        // small backoff before trying to reconnect
                        await Task.Delay(500, _runCts.Token);
                        await EnsureConnectedAsync(_runCts.Token);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Re-connect attempt failed.");
                    }
                }
            };
        }

        private static string? ExtractSlv(JsonElement payload)
        {
            if (payload.ValueKind == JsonValueKind.String)
                return payload.GetString();

            if (payload.ValueKind == JsonValueKind.Object)
            {
                if (payload.TryGetProperty("carteSlv", out var p1) && p1.ValueKind == JsonValueKind.String)
                    return p1.GetString();
                if (payload.TryGetProperty("slv", out var p2) && p2.ValueKind == JsonValueKind.String)
                    return p2.GetString();
            }
            return null;
        }
    }
}
