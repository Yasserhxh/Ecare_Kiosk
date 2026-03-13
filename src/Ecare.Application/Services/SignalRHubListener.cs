using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ecare.Application.Services;

public sealed class SignalRListenerOptions
{
    /// <summary>Base negotiate endpoint, e.g. https://localhost:52832/signalr/negotiate</summary>
    public string NegotiateEndpoint { get; set; } = default!;

    /// <summary>The hub name to listen on (passed to ?hub=...)</summary>
    public string Hub { get; set; } = default!;

    /// <summary>The event/method name to subscribe to on that hub</summary>
    public string Method { get; set; } = default!;
}

public interface ISignalRInboundHandler
{
    Task HandleAsync(object payload, CancellationToken ct);
}

public sealed class LoggingInboundHandler : ISignalRInboundHandler
{
    private readonly ILogger<LoggingInboundHandler> _log;

    public LoggingInboundHandler(ILogger<LoggingInboundHandler> log)
    {
        _log = log;
    }

    public Task HandleAsync(object payload, CancellationToken ct)
    {
        try
        {
            var json = payload is JsonElement je
                ? JsonSerializer.Serialize(je)
                : JsonSerializer.Serialize(payload);

            _log.LogInformation("Inbound payload: {json}", json);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to serialize inbound payload");
        }

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
        await Task.Delay(1000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConnectionLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "SignalR listener crashed; retrying in 5s");
                await SafeDelay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task RunConnectionLoopAsync(CancellationToken ct)
    {
        var (url, token) = await NegotiateAsync(_opt.Value.Hub, ct);

        var closedSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _conn = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token)!;
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10)
            })
            .Build();

        _conn.ServerTimeout = TimeSpan.FromSeconds(60);
        _conn.KeepAliveInterval = TimeSpan.FromSeconds(15);

        _conn.On<object>(_opt.Value.Method, payload => OnInboundAsync(payload, ct));

        _conn.Reconnecting += error =>
        {
            _log.LogWarning(error, "SignalR listener reconnecting. Hub={hub}, Method={method}",
                _opt.Value.Hub, _opt.Value.Method);
            return Task.CompletedTask;
        };

        _conn.Reconnected += connectionId =>
        {
            _log.LogInformation("SignalR listener reconnected. Hub={hub}, Method={method}, ConnectionId={connectionId}",
                _opt.Value.Hub, _opt.Value.Method, connectionId);
            return Task.CompletedTask;
        };

        _conn.Closed += error =>
        {
            _log.LogWarning(error, "SignalR listener closed. Hub={hub}, Method={method}",
                _opt.Value.Hub, _opt.Value.Method);

            closedSignal.TrySetResult(true);
            return Task.CompletedTask;
        };

        await _conn.StartAsync(ct);

        _log.LogInformation("SignalR listener connected. Hub={hub}, Method={method}, ConnectionId={connectionId}",
            _opt.Value.Hub, _opt.Value.Method, _conn.ConnectionId);

        using var reg = ct.Register(() => closedSignal.TrySetCanceled(ct));

        try
        {
            await closedSignal.Task;
        }
        finally
        {
            await DisposeConnectionAsync();
        }

        if (!ct.IsCancellationRequested)
        {
            _log.LogInformation("SignalR listener will renegotiate and rebuild connection in 5s");
            await SafeDelay(TimeSpan.FromSeconds(5), ct);
        }
    }

    private async Task<(string url, string token)> NegotiateAsync(string hub, CancellationToken ct)
    {
        var http = _http.CreateClient(nameof(SignalRHubListener));

        var listenerId = $"backend-listener-{hub}-{Environment.MachineName}";

        var negotiateUrl =
            $"{_opt.Value.NegotiateEndpoint}?hub={Uri.EscapeDataString(hub)}&deviceId={Uri.EscapeDataString(listenerId)}";

        _log.LogInformation("Negotiating at {url}", negotiateUrl);

        var resp = await http.GetFromJsonAsync<NegotiateResponse>(negotiateUrl, ct)
                   ?? throw new InvalidOperationException("Negotiate returned null.");

        if (string.IsNullOrWhiteSpace(resp.Url) || string.IsNullOrWhiteSpace(resp.AccessToken))
            throw new InvalidOperationException("Negotiate response missing url or accessToken.");

        return (resp.Url, resp.AccessToken);
    }

    private async Task OnInboundAsync(object payload, CancellationToken ct)
    {
        try
        {
            await _handler.HandleAsync(payload, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Inbound handler failed");
        }
    }

    private async Task DisposeConnectionAsync()
    {
        if (_conn is null)
            return;

        try
        {
            await _conn.StopAsync();
        }
        catch
        {
        }

        try
        {
            await _conn.DisposeAsync();
        }
        catch
        {
        }

        _conn = null;
    }

    private static async Task SafeDelay(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisposeConnectionAsync();
        await base.StopAsync(cancellationToken);
    }

    private sealed record NegotiateResponse(string Url, string AccessToken);
}