using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ecare.Application.Services;

public sealed class RfidListenerOptions
{
    public string NegotiateEndpoint { get; set; } = default!;
    public string SourceHub { get; set; } = "slv_pabentry_hub";
    public string SourceMethod { get; set; } = "ReceivePabEntryRfid";

    public string TargetHub { get; set; } = "pabentry_data_hub";
    public string TargetMethod { get; set; } = "PabEntryDataEvent";
}

public sealed class RfidPabEntryListner : BackgroundService
{
    private readonly ILogger<RfidPabEntryListner> _log;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _http;
    private readonly RfidListenerOptions _opt;
    private readonly ServiceManager _signalR;
    private HubConnection? _conn;

    public RfidPabEntryListner(
        ILogger<RfidPabEntryListner> log,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory http,
        IOptions<RfidListenerOptions> opt,
        ServiceManager signalR)
    {
        _log = log;
        _scopeFactory = scopeFactory;
        _http = http;
        _opt = opt.Value;
        _signalR = signalR;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1500, stoppingToken); // let Kestrel/negotiate come up if same process

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (url, token) = await NegotiateAsync(_opt.SourceHub, stoppingToken);
                _conn = new HubConnectionBuilder()
                    .WithUrl(url, o => o.AccessTokenProvider = () => Task.FromResult(token)!)
                    .WithAutomaticReconnect()
                    .Build();

                _conn.On<object>(_opt.SourceMethod, OnRfidAsync);

                _conn.Closed += async (ex) =>
                {
                    _log.LogWarning(ex, "SignalR listener disconnected; retrying in 5s...");
                    await Task.Delay(5000, stoppingToken);
                };

                await _conn.StartAsync(stoppingToken);
                _log.LogInformation("RFID listener connected to hub '{hub}'", _opt.SourceHub);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "RFID listener loop failed; retrying in 5s");
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
        var http = _http.CreateClient(nameof(RfidPabEntryListner));
        var negotiateUrl = $"{_opt.NegotiateEndpoint}?hub={Uri.EscapeDataString(hub)}";

        _log.LogInformation("Negotiating with {url}", negotiateUrl);
        var resp = await http.GetFromJsonAsync<NegotiateResponse>(negotiateUrl, ct)
                   ?? throw new InvalidOperationException("Negotiate returned null.");

        if (string.IsNullOrWhiteSpace(resp.Url) || string.IsNullOrWhiteSpace(resp.AccessToken))
            throw new InvalidOperationException("Negotiate response missing url or accessToken.");

        return (resp.Url, resp.AccessToken);
    }

    private async void OnRfidAsync(object payload)
    {
        try
        {
            var slv = ExtractCarteSlv(payload);
            if (string.IsNullOrWhiteSpace(slv))
            {
                _log.LogWarning("Invalid RFID payload: {payload}", JsonSerializer.Serialize(payload));
                return;
            }

            _log.LogInformation("RFID received CarteSLV={slv}", slv);

            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // Example query: replace with your actual one if different
            var result = await mediator.Send(new Ecare.Application.Queries.ScanBySlvQuery(slv));
            if (!result.Success || result.Value is null)
            {
                _log.LogWarning("Handler failed for SLV={slv}: {err}", slv, result.Error);
                return;
            }

            var vm = result.Value;

            var enriched = new
            {
                @event = "PabEntryDataEvent",
                site = "Asment-Temara-01",
                kiosk = "pab-entry-pc-01",
                slv = vm.CarteSLV,
                ts = DateTime.UtcNow,
                driver = new { id = vm.DriverId, plate = vm.Plate },
                client = new { name = vm.ClientName, sapOk = vm.SapOk },
                order = vm.Order is null ? null : new
                {
                    number = vm.Order.Number,
                    destination = vm.Order.Destination,
                    deliveryMode = vm.Order.DeliveryMode,
                    truckPlate = vm.Order.TruckPlate,
                    status = vm.Order.Status,
                    items = vm.Order.Items.Select(i => new
                    {
                        productId = i.ProductId,
                        productName = i.ProductName,
                        quantity = i.Quantity,
                        unite = i.Unite
                    })
                }
            };

            await SignalRHelper.BroadcastAsync(
                _signalR,
                hubName: _opt.TargetHub,
                methodName: _opt.TargetMethod,
                payload: enriched,
                logger: _log
            );

            _log.LogInformation("Broadcasted {method} to {hub} for SLV={slv}",
                _opt.TargetMethod, _opt.TargetHub, slv);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to process RFID payload");
        }
    }

    private static string? ExtractCarteSlv(object payload)
    {
        try
        {
            if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
                if (el.TryGetProperty("carteSlv", out var v)) return v.GetString();

            var pi = payload.GetType().GetProperty("carteSlv");
            return pi?.GetValue(payload)?.ToString();
        }
        catch { return null; }
    }

    private sealed record NegotiateResponse(string Url, string AccessToken);
}
