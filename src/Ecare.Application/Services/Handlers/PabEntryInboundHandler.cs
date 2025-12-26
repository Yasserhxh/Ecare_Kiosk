using Dapper;
using Ecare.Application.Queries.PabEntryScan;
using Ecare.Application.Services.Ecare.Application.Services;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

namespace Ecare.Application.Services
{

    public sealed class PabEntryOutboundOptions { public string Hub { get; set; } = "pabentry_data_hub"; public string Method { get; set; } = "PabEntryDataEvent"; }
    public sealed class PabEntryInboundHandler : ISignalRInboundHandler
    {
        private readonly ILogger<PabEntryInboundHandler> _log;
        private readonly IServiceProvider _sp;
        private readonly ServiceManager _signalR;
        private readonly PabEntryOutboundOptions _outOpt;

        public PabEntryInboundHandler(
            ILogger<PabEntryInboundHandler> log,
            IServiceProvider sp,
            ServiceManager signalR,
            IOptions<PabEntryOutboundOptions> outOpt)
        {
            _log = log;
            _sp = sp;
            _signalR = signalR;
            _outOpt = outOpt.Value;
        }

        public async Task HandleAsync(object payload, CancellationToken ct)
        {
            // --------------------------
            // Extract SLV + Device
            // --------------------------
            string? slv = TryExtractCarteSlv(payload) ?? "";
            string deviceId = TryExtractDeviceId(payload) ?? "UNKNOWN";

            _log.LogInformation("PabEntry: Received SLV={slv} from device={deviceId}", slv, deviceId);

            using var scope = _sp.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // --------------------------
            // Execute stored procedure
            // --------------------------
            var scan = await mediator.Send(new PabEntryScanQuery(slv), ct);

            // If SP returned NO DATA → build empty VM
            PabEntryScanVm vm =
                scan.Success && scan.Value is not null
                ? scan.Value
                : new PabEntryScanVm();

            // --------------------------
            // Build ALWAYS-FULL payload
            // --------------------------
             

            if (vm.Produit1 == "" || vm.Produit1.IsNullOrEmpty()) {
                return;
            }

           


            // --------------------------
            // Build NORMAL payload
            // --------------------------
            var outboundPayload = new
            {
                @event = "PabEntryDataEvent",
                site = "Asment-Temara-01",
                kiosk = deviceId,
                slv = slv,
                bonCommande = vm.BonDeCommande,
                ts = DateTime.UtcNow,

                driver = new
                {
                    id = vm.DriverId,
                    name = vm.FullName ?? "",
                    plate = vm.Matricule ?? ""
                },

                client = new
                {
                    name = vm.ClientName ?? "",
                    sapOk = (bool?)null
                },

                chantier = vm.Chantier ?? "",

                order = new
                {
                    produit1 = vm.Produit1,
                    quantite1 = vm.Quantite1,
                    produit2 = vm.Produit2,
                    quantite2 = vm.Quantite2
                },

                firstWeight = vm.PremierePoid,
                image1 = vm.Image1,
                image2 = vm.Image2
            };

            // --------------------------
            // SEND normal payload
            // --------------------------
            await SignalRHelper.BroadcastToDeviceAsync(
                _signalR,
                hubName: _outOpt.Hub,
                methodName: _outOpt.Method,
                deviceId: deviceId,
                payload: outboundPayload,
                logger: _log,
                ct: ct
            );

            _log.LogInformation("PabEntry: SENT normal payload SLV={slv} to device={deviceId}", slv, deviceId);
        }

        // ---------------------------------------------------------
        // Helpers FOR payload extraction
        // ---------------------------------------------------------
        private static string? TryExtractCarteSlv(object payload)
        {
            if (payload is JsonElement el)
            {
                if (el.TryGetProperty("carteSlv", out var v1) && v1.ValueKind == JsonValueKind.String)
                    return v1.GetString();
                if (el.TryGetProperty("slv", out var v2) && v2.ValueKind == JsonValueKind.String)
                    return v2.GetString();
            }

            return payload.GetType().GetProperty("carteSlv")?.GetValue(payload)?.ToString()
                ?? payload.GetType().GetProperty("slv")?.GetValue(payload)?.ToString();
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
}
