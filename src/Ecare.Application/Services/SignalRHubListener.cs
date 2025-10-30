    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    namespace Ecare.Application.Services
    {
        using Microsoft.AspNetCore.SignalR.Client;
        using Microsoft.Extensions.Hosting;
        using Microsoft.Extensions.Logging;
        using Microsoft.Extensions.Options;
        using System.Net.Http.Json;
        using System.Text.Json;

        namespace Ecare.Application.Services
        {
            public sealed class SignalRListenerOptions
            {
                /// <summary>Base negotiate endpoint, e.g. https://localhost:52831/signalr/negotiate</summary>
                public string NegotiateEndpoint { get; set; } = default!;
                /// <summary>The hub name to listen on (passed to ?hub=...)</summary>
                public string Hub { get; set; } = default!;
                /// <summary>The event/method name to subscribe to on that hub</summary>
                public string Method { get; set; } = default!;
            }

            /// <summary>Implement this to process the incoming payloads.</summary>
            public interface ISignalRInboundHandler
            {
                Task HandleAsync(object payload, CancellationToken ct);
            }

            /// <summary>Default no-op handler that just logs JSON.</summary>
            public sealed class LoggingInboundHandler : ISignalRInboundHandler
            {
                private readonly ILogger<LoggingInboundHandler> _log;
                public LoggingInboundHandler(ILogger<LoggingInboundHandler> log) => _log = log;

                public Task HandleAsync(object payload, CancellationToken ct)
                {
                    try
                    {
                        var json = payload is JsonElement je
                            ? JsonSerializer.Serialize(je)
                            : JsonSerializer.Serialize(payload);
                        _log.LogInformation("📩 Inbound payload: {json}", json);
                    }
                    catch { /* ignore serialization errors */ }
                    return Task.CompletedTask;
                }
            }

            public sealed class SignalRHubListener : BackgroundService
            {
                private readonly ILogger<SignalRHubListener> _log;
                private readonly IHttpClientFactory _http;
                private readonly IOptions<SignalRListenerOptions> _opt;
                private readonly ISignalRInboundHandler _handler;
                private HubConnection? _conn;

                public SignalRHubListener(
                    ILogger<SignalRHubListener> log,
                    IHttpClientFactory http,
                    IOptions<SignalRListenerOptions> opt,
                    ISignalRInboundHandler handler)
                {
                    _log = log;
                    _http = http;
                    _opt = opt;
                    _handler = handler;
                }

                protected override async Task ExecuteAsync(CancellationToken stoppingToken)
                {
                    // small delay if the same process hosts /signalr/negotiate
                    await Task.Delay(1000, stoppingToken);

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            var (url, token) = await NegotiateAsync(_opt.Value.Hub, stoppingToken);

                            _conn = new HubConnectionBuilder()
                                .WithUrl(url, o => o.AccessTokenProvider = () => Task.FromResult(token)!)
                                .WithAutomaticReconnect()
                                .Build();

                            _conn.On<object>(_opt.Value.Method, OnInbound);

                            _conn.Closed += async ex =>
                            {
                                _log.LogWarning(ex, "SignalR listener disconnected; reconnecting in 5s…");
                                try { await Task.Delay(5000, stoppingToken); } catch { }
                            };

                            await _conn.StartAsync(stoppingToken);
                            _log.LogInformation("✅ SignalR listener connected. Hub={hub}, Method={method}",
                                _opt.Value.Hub, _opt.Value.Method);

                            await Task.Delay(Timeout.Infinite, stoppingToken);
                        }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex)
                        {
                            _log.LogError(ex, "Listener error; retrying in 5s");
                            try { await Task.Delay(5000, stoppingToken); } catch { }
                        }
                    }
                }

                public override async Task StopAsync(CancellationToken cancellationToken)
                {
                    if (_conn is not null)
                    {
                        try { await _conn.StopAsync(cancellationToken); } catch { }
                    }
                    await base.StopAsync(cancellationToken);
                }

            private async Task<(string url, string token)> NegotiateAsync(string hub, CancellationToken ct)
            {
                var http = _http.CreateClient(nameof(SignalRHubListener));

                //  Generate a unique deviceId for this backend listener instance
                var listenerId = $"backend-listener-{hub}-{Guid.NewGuid():N}";

                //  Add deviceId to query string
                var negotiateUrl = $"{_opt.Value.NegotiateEndpoint}?hub={Uri.EscapeDataString(hub)}&deviceId={Uri.EscapeDataString(listenerId)}";

                _log.LogInformation("Negotiating at {url}", negotiateUrl);

                var resp = await http.GetFromJsonAsync<NegotiateResponse>(negotiateUrl, ct)
                           ?? throw new InvalidOperationException("Negotiate returned null.");

                if (string.IsNullOrWhiteSpace(resp.Url) || string.IsNullOrWhiteSpace(resp.AccessToken))
                    throw new InvalidOperationException("Negotiate response missing url or accessToken.");

                return (resp.Url, resp.AccessToken);
            }

            private async void OnInbound(object payload)
                {
                    try
                    {
                        await _handler.HandleAsync(payload, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Inbound handler failed");
                    }
                }

                private sealed record NegotiateResponse(string Url, string AccessToken);
            }
        }

    }
