using Dapper;
using Ecare.Application.Queries.MultiClientOrders;
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
            await SendError(deviceId, slv, "NO_DATA", ct);
            return;
        }



        // 1. Extract plate
        string truckPlate =
            result.Value.Clients
                .SelectMany(c => c.Chantiers)
                .Select(ch => ch.Order?.TruckPlate)
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? "";

        // 2. Check queue
        bool isInQueue = false;
        if (!string.IsNullOrWhiteSpace(truckPlate))
        {
            isInQueue = await CheckQueueFast(truckPlate, scope, ct);
        }

        // 3. Build payload (isInQueue exists HERE)
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

                    order = ch.Order is null ? null : new
                    {
                        OrderId = ch.Order.OrderId,
                        Number = ch.Order.Number,
                        Destination = ch.Order.Destination,
                        DeliveryMode = ch.Order.DeliveryMode,
                        TruckPlate = ch.Order.TruckPlate,
                        Status = ch.Order.Status,
                        items = ch.Order.Items.Select(i => new
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
        // Send to the specific kiosk/device
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
    // SMALL HELPERS
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
            FROM dbo.Ecare_Queue
            WHERE Matricule = @Plate AND Status IN (0,1);
        ";

        await using var conn = new SqlConnection(connStr);

        var found = await conn.ExecuteScalarAsync<int?>(sql, new { Plate = plate });

        return found.HasValue;
    }

    private async Task SendError(string deviceId, string slv, string error, CancellationToken ct)
    {
        var errPayload = new
        {
            type = 1,
            target = "OrderDataEvent",
            arguments = new object[]
            {
                new
                {
                    @event = "Error",
                    slv,
                    error
                }
            }
        };

        await SignalRHelper.BroadcastToDeviceAsync(
            _signalR,
            _opt.Hub,
            _opt.Method,
            deviceId,
            errPayload,
            _log,
            ct
        );
    }

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

        var p = payload.GetType().GetProperty("deviceId");
        return p?.GetValue(payload)?.ToString();
    }
}
