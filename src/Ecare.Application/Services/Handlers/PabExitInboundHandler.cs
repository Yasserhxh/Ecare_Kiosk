using Dapper;
using Ecare.Application.Queries;
using Ecare.Application.Queries.PabExitScan;
using Ecare.Application.Services.Ecare.Application.Services;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ecare.Application.Services.Handlers
{
    public sealed class PabExitOutboundOptions
    {
        public string Hub { get; set; } = "pabexit_data_hub";
        public string Method { get; set; } = "PabExitDataEvent";
    }
    public sealed class PabExitInboundHandler : ISignalRInboundHandler
    {
        private readonly ILogger<PabExitInboundHandler> _log;
        private readonly IServiceProvider _sp;
        private readonly ServiceManager _signalR;
        private readonly PabExitOutboundOptions _outOpt;

        public PabExitInboundHandler(
            ILogger<PabExitInboundHandler> log,
            IServiceProvider sp,
            ServiceManager signalR,
            IOptions<PabExitOutboundOptions> outOpt)
        {
            _log = log;
            _sp = sp;
            _signalR = signalR;
            _outOpt = outOpt.Value;
        }

        public async Task HandleAsync(object payload, CancellationToken ct)
        {
            string? rfid = TryExtractCarteSlv(payload);
            string? deviceId = TryExtractDeviceId(payload);

            if (string.IsNullOrWhiteSpace(rfid) || string.IsNullOrWhiteSpace(deviceId))
                return;

            using var scope = _sp.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var result = await mediator.Send(
                new PabExitScanQuery(rfid), ct);

            if (!result.Success || result.Value is null)
            {
                _log.LogWarning("Exit PAB: No data for RFID={rfid}", rfid);
                return;
            }

            var vm = result.Value;

            var outboundPayload = new
            {
                @event = "PabExitDataEvent",
                site = "Asment-Temara-01",
                kiosk = deviceId,
                slv = vm.CarteSLV,
                ts = DateTime.UtcNow,

                driver = new
                {
                    id = vm.DriverId,
                    name = vm.DriverFullName,
                    plate = vm.Plate
                },

                client = new
                {
                    name = vm.ClientName
                },

                produit1 = new
                {
                    name = vm.Produit1,
                    qty = vm.Quantite1,
                    image = vm.Produit1Image
                },

                produit2 = new
                {
                    name = vm.Produit2,
                    qty = vm.Quantite2,
                    image = vm.Produit2Image
                },

                premierePoid = vm.PremierePoid,
                deuxiemePoid = vm.DeuxiemePoid,
                ligne = vm.Ligne,
                bonDeCommande = vm.BonDeCommande,
                bonDeLivraison = vm.BonDeLivraison,

                times = new
                {
                    parking = vm.ParkingAt,
                    pabEntry = vm.PabEntryAt,
                    start = vm.StartChargingAt,
                    finish = vm.FinishedChargingAt,
                    exit = vm.PabExitAt,
                    elapsedParking = vm.ElapsedTimeParking,
                    elapsedCharging = vm.ElapsedInPab_Charging,
                    elapsedExit = vm.ElapsedTimeInF_Exit,
                    total = vm.TotalTimeInCercuit
                }
            };

            // broadcast to the device
            await SignalRHelper.BroadcastToDeviceAsync(
                _signalR,
                _outOpt.Hub,
                _outOpt.Method,
                deviceId,
                outboundPayload,
                _log,
                ct);

            _log.LogInformation("Exit PAB sent for RFID={rfid} device={device}", rfid, deviceId);
        }



        private static async Task<FluxSnapshot?> GetLatestValidFluxAsync(
        IServiceScope scope,
        string carteSlv,
        CancellationToken ct)
        {
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var connStr = config.GetConnectionString("SqlServer");
            if (string.IsNullOrWhiteSpace(connStr))
                throw new InvalidOperationException("Missing 'SqlServer' connection string.");

            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);

            const string sql = @"
         SELECT TOP(1)
             Ligne,
             TotalCharged,
              SecondWeight,
             FirstWeight
         FROM dbo.EcareFlux
         WHERE 
             CarteSlv = @CarteSlv
              
            AND PabExitAt IS NULL
         ORDER BY ParkedAt ;";

            return await conn.QueryFirstOrDefaultAsync<FluxSnapshot>(
                new CommandDefinition(
                    sql,
                    new { CarteSlv = carteSlv },
                    cancellationToken: ct));
        }

        private sealed class FluxSnapshot
        {
            public decimal? FirstWeight { get; init; }
            public decimal? SecondWeight { get; init; }
            public string Ligne { get; init; } = default!;
            public decimal? TotalCharged { get; init; }
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
}
