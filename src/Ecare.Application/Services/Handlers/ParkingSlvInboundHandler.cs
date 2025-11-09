using Dapper;
using Ecare.Application.Queries;
using Ecare.Application.Services.Ecare.Application.Services;

// using Ecare.Application.Services.Ecare.Application.Services; // <- looks accidental, you can remove
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

public sealed class ParkingOutboundOptions
{
    public string Hub { get; set; } = "pabentry_data_hub";
    public string Method { get; set; } = "PabEntryDataEvent";
}

public sealed class ParkingSlvInboundHandler : ISignalRInboundHandler
{
    private readonly ILogger<ParkingSlvInboundHandler> _log;
    private readonly IServiceProvider _sp;
    private readonly ServiceManager _signalR;
    private readonly ParkingOutboundOptions _outOpt;

    public ParkingSlvInboundHandler(
        ILogger<ParkingSlvInboundHandler> log,
        IServiceProvider sp,
        ServiceManager signalR,
        IOptions<ParkingOutboundOptions> outOpt)
    {
        _log = log;
        _sp = sp;
        _signalR = signalR;
        _outOpt = outOpt.Value;
    }

    public async Task HandleAsync(object payload, CancellationToken ct)
    {
        // Extract SLV
        string? slv = TryExtractCarteSlv(payload);
        if (string.IsNullOrWhiteSpace(slv))
        {
            _log.LogWarning("Missing carteSlv in payload: {raw}", JsonSerializer.Serialize(payload));
            return;
        }

        // Extract deviceId
        string? deviceId = TryExtractDeviceId(payload);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            _log.LogWarning("Missing deviceId in payload for SLV={slv}", slv);
            return;
        }

        _log.LogInformation("📥 Processing SLV={slv} from device={device}", slv, deviceId);

        using var scope = _sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new ScanBySlvQuery(slv), ct);
        if (!result.Success || result.Value is null)
        {
            _log.LogWarning("Scan failed for SLV={slv}: {err}", slv, result.Error);
            return;
        }

        var vm = result.Value;

        // === SELECT * + rows>0 => IsInQueue ====================================
        bool isInQueue = false;

        if (!string.IsNullOrWhiteSpace(vm.Plate))
        {
            try
            {
                var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var connStr =
                       cfg.GetConnectionString("SqlServer")
                    ?? cfg["ConnectionStrings:SqlServer"]
                    ?? cfg["Db:ConnectionStrings:SqlServer"];

                const string sql = @"
                SELECT *
                FROM dbo.Ecare_Queue 
                WHERE Matricule = @Plate
                ";

                await using var conn = new SqlConnection(connStr);

                // We don't need to map a type; just check if any row comes back
                var rows = await conn.QueryAsync(
                    new CommandDefinition(
                        sql,
                        new { Plate = vm.Plate },
                        cancellationToken: ct));

                isInQueue = rows.AsList().Count > 0;

                _log.LogInformation("Queue check: order {order} / plate {plate} => IsInQueue={inQueue}");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed EcareFlux check for order={order}, plate={plate}",
                    vm.Order?.Number, vm.Plate);
            }
        }
        // =======================================================================

        var outboundPayload = new
        {
            @event = "OrderDataEvent",
            site = "Asment-Temara-01",
            kiosk = "parking-pc-01",
            slv = vm.CarteSLV,
            ts = DateTime.UtcNow,

            // Capitalized as requested
            IsInQueue = isInQueue,

            driver = new
            {
                id = vm.DriverId,
                name = vm.DriverName,
                plate = vm.Plate
            },
            client = new
            {
                name = vm.ClientName,
                sapOk = vm.SapOk
            },
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
                    unite = i.Unite,
                    imageUrl = i.ImageUrl
                })
            }
        };

        await SignalRHelper.BroadcastToDeviceAsync(
            _signalR,
            hubName: _outOpt.Hub,
            methodName: _outOpt.Method,
            deviceId: deviceId,
            payload: outboundPayload,
            logger: _log,
            ct: ct
        );

        _log.LogInformation(" Sent OrderDataEvent to device={device}", deviceId);
    }

    private static string? TryExtractCarteSlv(object payload)
    {
        if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("carteSlv", out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
            if (el.TryGetProperty("slv", out var v2) && v2.ValueKind == JsonValueKind.String)
                return v2.GetString();
        }

        var p = payload.GetType().GetProperty("carteSlv")
                 ?? payload.GetType().GetProperty("slv");
        return p?.GetValue(payload)?.ToString();
    }

    private static string? TryExtractDeviceId(object payload)
    {
        if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("deviceId", out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }

        var p = payload.GetType().GetProperty("deviceId");
        return p?.GetValue(payload)?.ToString();
    }
}
