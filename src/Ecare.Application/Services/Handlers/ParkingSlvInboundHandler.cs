using Dapper;
using Ecare.Application.Queries;
using Ecare.Application.Queries.ParkingScan;
using Ecare.Application.Services.Ecare.Application.Services;
using Ecare.Domain.ValueObjects;
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
    private readonly ParkingOutboundOptions _opt;

    public ParkingSlvInboundHandler(
        ILogger<ParkingSlvInboundHandler> log,
        IServiceProvider sp,
        ServiceManager mgr,
        IOptions<ParkingOutboundOptions> opt)
    {
        _log = log;
        _sp = sp;
        _signalR = mgr;
        _opt = opt.Value;
    }

    public async Task HandleAsync(object payload, CancellationToken ct)
    {
        // -------- PARSE INPUT ----------------
        if (payload is not ParkingInboundDto dto)
        {
            _log.LogWarning("Invalid inbound payload");
            return;
        }

        string? slv = dto.carteSlv ?? dto.slv;
        string? deviceId = dto.deviceId;

        if (string.IsNullOrWhiteSpace(slv) || string.IsNullOrWhiteSpace(deviceId))
        {
            _log.LogWarning("Missing SLV or deviceId");
            return;
        }

        using var scope = _sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var scan = await mediator.Send(new ScanByRfidQuery(slv), ct);

        if (scan.Code == "RFID_NOT_FOUND")
        {
            await Send(deviceId, new { @event = "Error", slv, error = "RFID_NOT_FOUND" }, ct);
            return;
        }

        // ==========================================================
        // CASE 1: MULTIPLE CLIENTS
        // ==========================================================
        if (scan.Clients.Count > 1)
        {
            await Send(deviceId, new
            {
                @event = "SelectClient",
                slv,
                clients = scan.Clients
            }, ct);

            _log.LogInformation("MULTIPLE_CLIENTS → Sent SelectClient");
            return;
        }

        // ==========================================================
        // CASE 2: SINGLE CLIENT + MULTIPLE CHANTIERS
        // ==========================================================
        if (scan.Clients.Count == 1 && scan.Chantiers.Count > 1)
        {
            await Send(deviceId, new
            {
                @event = "SelectChantier",
                slv,
                client = scan.Clients[0],
                chantiers = scan.Chantiers
            }, ct);

            _log.LogInformation("MULTIPLE_CHANTIERS → Sent SelectChantier");
            return;
        }

        // ==========================================================
        // CASE 3: FULL FLOW (ONE CLIENT + ONE CHANTIER)
        // ==========================================================
        bool isInQueue = await CheckQueueAsync(scan.Truck?.Matricule, cfg);

        await Send(deviceId, new
        {
            @event = "OrderDataEvent",
            slv,
            ts = DateTime.UtcNow,
            IsInQueue = isInQueue,

            driver = scan.Truck is null ? null : new
            {
                id = scan.Truck.TruckId,
                name = $"{scan.Truck.DriverPrenom} {scan.Truck.DriverNom}",
                plate = scan.Truck.Matricule
            },

            client = scan.Clients.FirstOrDefault(),
            chantier = scan.Chantiers.FirstOrDefault(),
            order = scan.Order,
            items = scan.Items

        }, ct);
    }

    // ----------------- SMALL HELPERS -----------------
    private Task Send(string deviceId, object payload, CancellationToken ct)
    {
        return SignalRHelper.BroadcastToDeviceAsync(
            _signalR, _opt.Hub, _opt.Method, deviceId, payload, _log, ct);
    }

    private static async Task<bool> CheckQueueAsync(string? plate, IConfiguration cfg)
    {
        if (string.IsNullOrWhiteSpace(plate)) return false;

        const string sql = @"
            IF EXISTS (
                SELECT 1 FROM dbo.Ecare_Queue
                WHERE Matricule = @Plate AND Status IN (0,1)
            ) SELECT 1 ELSE SELECT 0;
        ";

        await using var conn = new SqlConnection(cfg.GetConnectionString("SqlServer"));
        return await conn.ExecuteScalarAsync<bool>(sql, new { Plate = plate });
    }
}


