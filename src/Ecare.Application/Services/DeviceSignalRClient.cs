using Ecare.Application.Queries;
using Ecare.Application.Services;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

public sealed class DeviceSignalRClient : IHostedService
{
    private readonly ILogger<DeviceSignalRClient> _log;
    private readonly SignalRClientOptions _opt;
    private readonly ServiceManager _serviceManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrderDataPublisher _publisher;
    private HubConnection? _conn;
    private readonly HttpClient _http = new();

    public DeviceSignalRClient(
        ILogger<DeviceSignalRClient> log,
        IOptions<SignalRClientOptions> opt,
        ServiceManager serviceManager,
        IServiceScopeFactory scopeFactory,
        OrderDataPublisher publisher)
    {
        _log = log;
        _opt = opt.Value;
        _serviceManager = serviceManager;
        _scopeFactory = scopeFactory;
        _publisher = publisher;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000, ct); // wait for Kestrel
            await RunAsync(ct);
        }, ct);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            _log.LogInformation("Negotiating SignalR connection via {url}", _opt.NegotiateEndpoint);
            var negotiateUrl = $"{_opt.NegotiateEndpoint}?hub={_opt.HubName}";

            var resp = await _http.GetFromJsonAsync<NegotiateResponse>(negotiateUrl, ct);
            if (resp is null || string.IsNullOrWhiteSpace(resp.Url))
                throw new InvalidOperationException("Negotiation failed");

            _log.LogInformation("Negotiation OK. Connecting to Azure SignalR...");

            await _publisher.InitializeAsync(ct);

            _conn = new HubConnectionBuilder()
                .WithUrl(resp.Url, o => o.AccessTokenProvider = () => Task.FromResult(resp.AccessToken)!)
                .WithAutomaticReconnect()
                .Build();

            _conn.On<object>(_opt.MethodName, async payload =>
            {
                try
                {
                    var slv = ExtractCarteSlv(payload);
                    if (string.IsNullOrWhiteSpace(slv))
                    {
                        _log.LogWarning("Invalid RFID payload: {payload}", payload);
                        return;
                    }

                    _log.LogInformation("RFID Received CarteSLV={slv}", slv);

                    using var scope = _scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var result = await mediator.Send(new ScanBySlvQuery(slv));

                    if (!result.Success)
                    {
                        _log.LogWarning("ScanBySlvHandler failed: {msg}", result.Error);
                        return;
                    }

                    var vm = result.Value;
                    if (vm is null)
                    {
                        _log.LogWarning("ScanBySlvHandler returned null result for SLV={slv}", slv);
                        return;
                    }

                    var enriched = new
                    {
                        @event = "OrderDataEvent",
                        site = "Asment-Temara-01",
                        kiosk = "parking",
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

                    await _publisher.BroadcastOrderDataAsync(enriched);
                    _log.LogInformation("✅ Broadcasted OrderDataEvent for SLV={slv}", slv);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Error while processing RFID payload");
                }
            });

            await _conn.StartAsync(ct);
            _log.LogInformation("SignalR listener connected successfully to {hub}", _opt.HubName);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "DeviceSignalRClient startup failed");
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_conn is not null)
            await _conn.StopAsync(ct);
    }

    private static string? ExtractCarteSlv(object payload)
    {
        try
        {
            if (payload is JsonElement json && json.ValueKind == JsonValueKind.Object)
                if (json.TryGetProperty("carteSlv", out var prop))
                    return prop.GetString();

            var type = payload.GetType();
            var propInfo = type.GetProperty("carteSlv");
            return propInfo?.GetValue(payload)?.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ExtractCarteSlv] Error: {ex.Message}");
            return null;
        }
    }

    public sealed record NegotiateResponse(string Url, string AccessToken);
}

public sealed class SignalRClientOptions
{
    public string NegotiateEndpoint { get; set; } = default!;
    public string HubName { get; set; } = "slv_hub";
    public string MethodName { get; set; } = "ReceiveRfid";
}
