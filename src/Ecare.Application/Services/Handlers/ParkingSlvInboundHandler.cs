using Ecare.Application.Queries.ParkingScanOrderLegend;  // ParkingScanQuery
using Ecare.Application.Services.Ecare.Application.Services;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Text.Json;
using static Ecare.Application.Queries.ParkingScanOrderLegend.ParkingScanModels;

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

    // ======================================================================
    // MAIN ENTRY POINT
    // ======================================================================
    public async Task HandleAsync(object payload, CancellationToken ct)
    {
        string? slv = TryExtractCarteSlv(payload);
        string? deviceId = TryExtractDeviceId(payload);

        if (string.IsNullOrWhiteSpace(slv))
        {
            _log.LogWarning("ParkingInbound: Missing SLV in payload");
            return;
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            _log.LogWarning("ParkingInbound: Missing deviceId for SLV={slv}", slv);
            return;
        }

        _log.LogInformation("ParkingInbound: SLV={slv} device={deviceId}", slv, deviceId);

        using var scope = _sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Call query that returns one of the CASES
        var response = await mediator.Send(new ParkingScanQuery(slv), ct);

        if (!response.Success || response.Value == null)
        {
            _log.LogWarning("ParkingInbound: No scan result for SLV={slv}", slv);
            return;
        }

        var scan = response.Value;

        // Build frontend-friendly payload
        var outboundPayload = BuildParkingPayload(slv, deviceId, scan);

        // BROADCAST
        await SignalRHelper.BroadcastToDeviceAsync(
            _signalR,
            _opt.Hub,
            _opt.Method,
            deviceId,
            outboundPayload,
            _log,
            ct
        );

        _log.LogInformation("ParkingInbound: Sent payload for SLV={slv} -> device={deviceId}", slv, deviceId);
    }

    // ======================================================================
    // 5-CASE PAYLOAD BUILDER (includes MANY_DRIVERS)
    // ======================================================================
    private object BuildParkingPayload(string slv, string deviceId, ScanResultVm scan)
    {
        // -----------------------------------------
        // CASE 1 — NO CLIENTS + NO ORDER
        // -----------------------------------------
        if (scan.Clients == null || scan.Clients.Count == 0)
        {
            return new
            {
                @event = "NO_ORDER_NO_CLIENT",
                slv
            };
        }

        // -----------------------------------------
        // NEW CASE — SLV USED BY MULTIPLE CLIENTS / DRIVERS
        // MANY_DRIVERS
        // -----------------------------------------
        if (scan.Clients.Count > 1)
        {
            // For each driver we send:
            // - Matricule
            // - ChauffeurName
            // - ClientName + CodeSapClient
            // - Chantiers (if no order or still loaded)
            // - Order if found (same logic as existing)
            return new
            {
                @event = "MANY_DRIVERS",
                slv,
                drivers = scan.Clients.Select(c => new
                {
                    clientName = c.ClientName,
                    codeClientSAP = c.CodeSapClient,
                    matricule = c.Matricule,
                    chauffeur = c.ChauffeurName,
                    hasOrder = c.Order != null,
                    order = c.Order,  // can be null
                    chantiers = c.Chantiers.Select(ch => new
                    {
                        codeSapChantier = ch.CodeSapChantier,
                        nomChantier = ch.NomChantier
                    })
                })
            };
        }

        // From here we know there is EXACTLY ONE client row
        var single = scan.Clients[0];

        // -----------------------------------------
        // CASE 2 — ORDER FOUND (single client)
        // -----------------------------------------
        if (single.Order != null)
        {
            return new
            {
                @event = "ORDER_FOUND",
                slv,
                order = single.Order,
                chauffeur = single.ChauffeurName,
                matricule = single.Matricule
            };
        }

        // -----------------------------------------
        // CASE 4 — Equipment exists BUT ClientName is NULL or EMPTY
        // -----------------------------------------
        if (string.IsNullOrWhiteSpace(single.ClientName))
        {
            return new
            {
                @event = "NO_CLIENT_BUT_EQUIPMENT_FOUND",
                slv,
                matricule = single.Matricule,
                chauffeur = single.ChauffeurName
            };
        }

        // -----------------------------------------
        // CASE 3 — ONE CLIENT + CHANTIERS (NO ORDER)
        // -----------------------------------------
        return new
        {
            @event = "CLIENTS_WITH_CHANTIERS",
            slv,
            clients = new[]
            {
                new
                {
                    clientName = single.ClientName,
                    codeClientSAP = single.CodeSapClient,
                    matricule = single.Matricule,
                    chauffeur = single.ChauffeurName,
                    chantiers = single.Chantiers.Select(ch => new
                    {
                        codeSapChantier = ch.CodeSapChantier,
                        nomChantier = ch.NomChantier
                    })
                }
            }
        };
    }

    // ======================================================================
    // HELPERS
    // ======================================================================
    private static string? TryExtractCarteSlv(object payload)
    {
        if (payload is JsonElement el)
        {
            if (el.TryGetProperty("carteSlv", out var c1)) return c1.GetString();
            if (el.TryGetProperty("slv", out var c2)) return c2.GetString();
        }

        return payload.GetType().GetProperty("slv")?.GetValue(payload)?.ToString()
            ?? payload.GetType().GetProperty("carteSlv")?.GetValue(payload)?.ToString();
    }

    private static string? TryExtractDeviceId(object payload)
    {
        if (payload is JsonElement el && el.TryGetProperty("deviceId", out var v))
            return v.GetString();

        return payload.GetType()
            .GetProperty("deviceId")
            ?.GetValue(payload)
            ?.ToString();
    }
}
