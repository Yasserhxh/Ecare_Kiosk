using Dapper;
using Ecare.Application.Queries;
using Ecare.Application.Services.Ecare.Application.Services;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
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
            // Extract SLV
            string? slv = TryExtractCarteSlv(payload);
            if (string.IsNullOrWhiteSpace(slv))
            {
                _log.LogWarning("PabExitInboundHandler: payload missing carteSlv. Raw={raw}",
                    payload is JsonElement je ? JsonSerializer.Serialize(je) : payload.ToString());
                return;
            }

            // Extract deviceId
            string? deviceId = TryExtractDeviceId(payload);
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                _log.LogWarning("PabExitInboundHandler: Missing deviceId for SLV={slv}", slv);
                return;
            }

            _log.LogInformation("PabExit: Processing SLV={slv} from device={device}", slv, deviceId);

            using var scope = _sp.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new ScanBySlvQuery(slv), ct);
            if (!result.Success || result.Value is null)
            {
                _log.LogWarning("PabExit: Scan failed for SLV={slv}. Err={err}", slv, result.Error);
                return;
            }

            var vm = result.Value;

            decimal? firstWeight = null;

            try
            {
                var orderNumber = vm.Order?.Number;

                if (!string.IsNullOrWhiteSpace(orderNumber))
                {
                    // Resolve UoW (or your connection provider) from the same scope
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    const string SqlFirstWeight = @"
            SELECT TOP(1) FirstWeight
            FROM dbo.EcareFlux WITH (NOLOCK)
            WHERE BonDeCommande = @BonDeCommande
            ORDER BY Id DESC;";

                    // Allow both numeric and string BonDeCommande
                    object param =
                        long.TryParse(orderNumber, out var bonNumeric)
                            ? new { BonDeCommande = bonNumeric }
                            : new { BonDeCommande = (object)orderNumber! };

                    firstWeight = await uow.Connection.QueryFirstOrDefaultAsync<decimal?>(SqlFirstWeight, param);
                    _log.LogInformation("PabExit: Found FirstWeight={firstWeight} for BDC={bdc}", firstWeight, orderNumber);
                }
                else
                {
                    _log.LogWarning("PabExit: vm.Order.Number is null/empty; skipping FirstWeight lookup.");
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "PabExit: Failed to fetch FirstWeight from EcareFlux.");
            }



            var outboundPayload = new
            {
                @event = "PabExitDataEvent",
                site = "Asment-Temara-01",
                firstWeight = firstWeight,
                kiosk = deviceId,
                slv = vm.CarteSLV,
                ts = DateTime.UtcNow,
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
                order = vm.Order
            };

            // Broadcast to specific device
            await SignalRHelper.BroadcastToDeviceAsync(
                _signalR,
                hubName: _outOpt.Hub,
                methodName: _outOpt.Method,
                deviceId: deviceId,
                payload: outboundPayload,
                logger: _log,
                ct: ct
            );

            _log.LogInformation("PabExit: Sent to device={device}", deviceId);
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
