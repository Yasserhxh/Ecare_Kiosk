// SignalRHubListener.cs
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Ecare.Application.Services
{
    public sealed class SignalRListenerOptions
    {
        public string NegotiateEndpoint { get; set; } = default!;
        public string ProofEndpoint { get; set; } = "http://127.0.0.1:5858/device-proof";
        public string Hub { get; set; } = default!;      // inbound hub (listen)
        public string Method { get; set; } = default!;   // inbound method (listen)
        public string? OutHub { get; set; }              // outbound hub (publish). If null => Hub
        public string OutMethod { get; set; } = default!;// outbound method (publish)
    }

    public interface ISignalRInboundHandler { Task HandleAsync(object payload, CancellationToken ct); }

    public sealed class LoggingInboundHandler : ISignalRInboundHandler
    {
        private readonly ILogger<LoggingInboundHandler> _log;
        public LoggingInboundHandler(ILogger<LoggingInboundHandler> log) => _log = log;
        public Task HandleAsync(object payload, CancellationToken ct)
        {
            try
            {
                var json = payload is JsonElement je ? JsonSerializer.Serialize(je) : JsonSerializer.Serialize(payload);
                _log.LogInformation("📩 Inbound: {json}", json);
            }
            catch { }
            return Task.CompletedTask;
        }
    }

    public sealed class SignalRHubListener : BackgroundService
    {
        private readonly ILogger<SignalRHubListener> _log;
        private readonly IHttpClientFactory _http;
        private readonly IOptions<SignalRListenerOptions> _opt;
        private readonly ISignalRInboundHandler _handler;
        private readonly ServiceManager _manager;  // concrete, matches your DI
        private HubConnection? _conn;

        public SignalRHubListener(
            ILogger<SignalRHubListener> log,
            IHttpClientFactory http,
            IOptions<SignalRListenerOptions> opt,
            ISignalRInboundHandler handler,
            ServiceManager manager)
        { _log = log; _http = http; _opt = opt; _handler = handler; _manager = manager; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(1000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var inHub = _opt.Value.Hub ?? throw new InvalidOperationException("Options:Hub missing.");
                    var (url, token) = await NegotiateWithProofAsync(inHub, stoppingToken);

                    _conn = new HubConnectionBuilder()
                        .WithUrl(url, o => o.AccessTokenProvider = () => Task.FromResult(token)!)
                        .WithAutomaticReconnect()
                        .Build();

                    _conn.On<object>(_opt.Value.Method, OnInbound);

                    _conn.Closed += async ex =>
                    {
                        _log.LogWarning(ex, "Listener disconnected; retrying…");
                        try { await Task.Delay(5000, stoppingToken); } catch { }
                    };

                    await _conn.StartAsync(stoppingToken);
                    _log.LogInformation("✅ API listening: Hub={hub}, Method={method}", inHub, _opt.Value.Method);

                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Listener error; retrying…");
                    try { await Task.Delay(5000, stoppingToken); } catch { }
                }
            }
        }

        public override async Task StopAsync(CancellationToken ct)
        {
            if (_conn is not null) { try { await _conn.StopAsync(ct); } catch { } }
            await base.StopAsync(ct);
        }

        private async void OnInbound(object payload)
        {
            try
            {
                await _handler.HandleAsync(payload, CancellationToken.None);

                var deviceId = ExtractDeviceIdOrThrow(payload);
                var outHub = string.IsNullOrWhiteSpace(_opt.Value.OutHub) ? _opt.Value.Hub : _opt.Value.OutHub!;
                var outMethod = _opt.Value.OutMethod;

                await SignalRHelper.SendToDeviceGroupAsync(
                    _manager, outHub, outMethod, deviceId, payload, _log, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Inbound processing/publish failed");
            }
        }

        private static string ExtractDeviceIdOrThrow(object payload)
        {
            if (payload is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Object && je.TryGetProperty("deviceId", out var d) && d.ValueKind == JsonValueKind.String)
                    return d.GetString()!;
            }
            var p = payload.GetType().GetProperty("deviceId") ?? payload.GetType().GetProperty("DeviceId");
            var v = p?.GetValue(payload)?.ToString();
            if (!string.IsNullOrWhiteSpace(v)) return v!;
            throw new InvalidOperationException("Inbound payload missing 'deviceId'.");
        }

        private async Task<(string url, string token)> NegotiateWithProofAsync(string hub, CancellationToken ct)
        {
            var http = _http.CreateClient(nameof(SignalRHubListener));
            var proof = await FetchDeviceProofAsync(http, ct);

            var negotiateUrl = new StringBuilder(_opt.Value.NegotiateEndpoint)
                .Append("?hub=").Append(Uri.EscapeDataString(hub))
                .Append("&deviceId=").Append(Uri.EscapeDataString(proof.deviceId))
                .Append("&ts=").Append(proof.ts)
                .Append("&nonce=").Append(Uri.EscapeDataString(proof.nonce))
                .Append("&sig=").Append(Uri.EscapeDataString(proof.sig))
                .ToString();

            _log.LogInformation("Negotiating at {url}", negotiateUrl);

            var resp = await http.GetFromJsonAsync<NegotiateResponse>(negotiateUrl, ct)
                       ?? throw new InvalidOperationException("Negotiate returned null.");

            if (string.IsNullOrWhiteSpace(resp.Url) || string.IsNullOrWhiteSpace(resp.AccessToken))
                throw new InvalidOperationException("Negotiate response missing url or accessToken.");

            return (resp.Url, resp.AccessToken);
        }

        private async Task<(string deviceId, long ts, string nonce, string sig)> FetchDeviceProofAsync(HttpClient http, CancellationToken ct)
        {
            var proofUrl = _opt.Value.ProofEndpoint ?? "http://127.0.0.1:5858/device-proof";
            var doc = await http.GetFromJsonAsync<DeviceProof>(proofUrl, ct)
                      ?? throw new InvalidOperationException("device-proof returned null.");

            if (string.IsNullOrWhiteSpace(doc.deviceId) ||
                string.IsNullOrWhiteSpace(doc.nonce) ||
                string.IsNullOrWhiteSpace(doc.sig) ||
                string.IsNullOrWhiteSpace(doc.ts))
                throw new InvalidOperationException("device-proof missing fields.");

            if (!long.TryParse(doc.ts, out var tsLong))
                throw new InvalidOperationException("device-proof ts must be unix seconds.");

            return (doc.deviceId, tsLong, doc.nonce, doc.sig);
        }

        private sealed record DeviceProof(string deviceId, string ts, string nonce, string sig);
        private sealed record NegotiateResponse(string Url, string AccessToken);
    }
}
