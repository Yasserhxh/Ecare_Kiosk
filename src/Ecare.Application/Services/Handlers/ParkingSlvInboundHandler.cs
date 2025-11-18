using Dapper;
using Ecare.Application.Queries.MultiClientOrders;
using Ecare.Application.Queries.MultiClientOrders.Ecare.Application.Queries.MultiClientOrders;
using Ecare.Application.Services.Ecare.Application.Services;
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
    public string Hub { get; set; } = "order_data_hub";
    public string Method { get; set; } = "OrderDataEvent";
}

public sealed class ParkingSlvInboundHandler : ISignalRInboundHandler
{
    private readonly ILogger<ParkingSlvInboundHandler> _log;
    private readonly IServiceProvider _sp;
    private readonly ServiceManager _signalR;
    private readonly ParkingOutboundOptions _opt;

    public ParkingSlvInboundHandler(
        ILogger<ParkingSlvInboundHandler> log,
        IServiceProvider sp,
        ServiceManager signalR,
        IOptions<ParkingOutboundOptions> opt)
    {
        _log = log;
        _sp = sp;
        _signalR = signalR;
        _opt = opt.Value;
    }

    // ============================================================
    // MAIN ENTRY POINT
    // ============================================================
    public async Task HandleAsync(object payload, CancellationToken ct)
    {
        string? slv = TryExtractCarteSlv(payload);
        string? deviceId = TryExtractDeviceId(payload);

        if (string.IsNullOrWhiteSpace(slv))
        {
            _log.LogWarning("❌ Missing SLV in inbound payload");
            return;
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            _log.LogWarning("❌ Missing deviceId for SLV={slv}", slv);
            return;
        }

        _log.LogInformation("📥 Received inbound parking scan SLV={slv} device={deviceId}", slv, deviceId);

        using var scope = _sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // ============================================================
        // Execute query handler (reads stored procedure)
        // ============================================================
        var result = await mediator.Send(new MultiClientOrdersQuery(slv), ct);

        if (!result.Success || result.Value is null)
        {
            _log.LogWarning("❌ No data returned for SLV={slv}", slv);
            return;
        }

        // 1. Extract plate
        string truckPlate = result.Value.Driver.Plate ?? "";

        // 2. Check queue (NOW USING Ecare_Order_Legend)
        bool isInQueue = false;
        
        
        isInQueue = await CheckQueueFast(truckPlate, scope, ct);
        

        // ============================================================
        // BUILD PAYLOAD
        // ============================================================
        var outboundPayload = new
        {
            type = 1,
            target = "OrderDataEvent",
            arguments = new[]
            {
                new
                {
                    @event = "MultiClientOrders",
                    slv = result.Value.Slv,
                    isInQueue = isInQueue,

                    driver = new
                    {
                        id = result.Value.Driver.DriverId,
                        fullName = result.Value.Driver.FullName,
                        plate = result.Value.Driver.Plate,
                    },

                    clients = result.Value.Clients.Select(c => new
                    {
                        ClientId = c.ClientId,
                        ClientCode = c.ClientCode,
                        ClientName = c.ClientName,

                        chantiers = c.Chantiers.Select(ch => new
                        {
                            ChantierId = ch.ChantierId,
                            ChantierCode = ch.ChantierCode,
                            ChantierName = ch.ChantierName,

                            order = ch.Order == null ? null : new
                            {
                                OrderId = ch.Order.OrderId,
                                Number = ch.Order.Number,
                                Destination = ch.Order.Destination,
                                DeliveryMode = ch.Order.DeliveryMode,
                                TruckPlate = ch.Order.TruckPlate,
                                Status = ch.Order.Status,

                                // NEW — queue status per order
                                isInQueue = isInQueue,

                                items = (ch.Order.Items ?? new List<OrderItemNode>())
                                    .Select(i => new
                                    {
                                        ProductId = i.ProductId,
                                        ProductName = i.ProductName,
                                        Quantity = i.Quantity,
                                        Unite = i.Unite,
                                        ImageUrl = i.ImageUrl
                                    })
                            }
                        })
                    })
                }
            }
        };

        // ============================================================
        // SEND TO SPECIFIC DEVICE
        // ============================================================
        await SignalRHelper.BroadcastToDeviceAsync(
            _signalR,
            _opt.Hub,
            _opt.Method,
            deviceId,
            outboundPayload,
            _log,
            ct
        );

        _log.LogInformation("Sent MultiClientOrders for SLV={slv} to device={deviceId}", slv, deviceId);
    }

    // ============================================================
    // QUEUE CHECK (NOW IN Ecare_Order_Legend)
    // ============================================================
    private async Task<bool> CheckQueueFast(string? plate, IServiceScope scope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plate))
            return false;

        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var connStr =
            cfg.GetConnectionString("SqlServer")
            ?? cfg["ConnectionStrings:SqlServer"]
            ?? cfg["Db:ConnectionStrings:SqlServer"];

        const string sql = @"
            SELECT TOP 1 1
            FROM dbo.Ecare_Order_Legend
            WHERE Matricule = @Plate
              AND Step = 1;   -- waiting in parking
        ";

        await using var conn = new SqlConnection(connStr);
        var found = await conn.ExecuteScalarAsync<int?>(sql, new { Plate = plate });
        return found.HasValue;
    }

    // ============================================================
    // HELPERS
    // ============================================================
    private static string? TryExtractCarteSlv(object payload)
    {
        if (payload is JsonElement el)
        {
            if (el.TryGetProperty("carteSlv", out var v1) && v1.ValueKind == JsonValueKind.String)
                return v1.GetString();
            if (el.TryGetProperty("slv", out var v2) && v2.ValueKind == JsonValueKind.String)
                return v2.GetString();
        }

        var p = payload.GetType().GetProperty("carteSlv")
              ?? payload.GetType().GetProperty("slv");

        return p?.GetValue(payload)?.ToString();
    }

    private static string? TryExtractDeviceId(object payload)
    {
        if (payload is JsonElement el &&
            el.TryGetProperty("deviceId", out var v) &&
            v.ValueKind == JsonValueKind.String)
        {
            return v.GetString();
        }

        return payload.GetType().GetProperty("deviceId")?.GetValue(payload)?.ToString();
    }
}
