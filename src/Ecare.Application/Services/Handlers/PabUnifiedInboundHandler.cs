
    using Dapper;
    using MediatR;
    using Microsoft.Azure.SignalR.Management;
    using Microsoft.Data.SqlClient;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System.Text.Json;
using Ecare.Application.Services;
using Ecare.Application.Services.Handlers;


namespace Ecare.Application.Services
    {
        public sealed class PabUnifiedInboundHandler : ISignalRInboundHandler
        {
            private readonly ILogger<PabUnifiedInboundHandler> _log;
            private readonly IServiceProvider _sp;
            private readonly ServiceManager _signalR;
            private readonly PabEntryOutboundOptions _entryOpt;
            private readonly PabExitOutboundOptions _exitOpt;

            public PabUnifiedInboundHandler(
                ILogger<PabUnifiedInboundHandler> log,
                IServiceProvider sp,
                ServiceManager signalR,
                IOptions<PabEntryOutboundOptions> entryOpt,
                IOptions<PabExitOutboundOptions> exitOpt)
            {
                _log = log;
                _sp = sp;
                _signalR = signalR;
                _entryOpt = entryOpt.Value;
                _exitOpt = exitOpt.Value;
            }

            public async Task HandleAsync(object payload, CancellationToken ct)
            {
                string slv = TryExtractSlv(payload) ?? "";
                string deviceId = TryExtractDeviceId(payload) ?? "UNKNOWN";

                _log.LogInformation(
                    "PabUnified: SLV={slv} Device={deviceId}",
                     slv, deviceId);

                using var scope = _sp.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                await using var conn = new SqlConnection(config.GetConnectionString("SqlServer"));
                await conn.OpenAsync(ct);

            var step = await conn.QueryFirstOrDefaultAsync<int>(
new CommandDefinition(
    "SELECT TOP (1) Step FROM Ecare_Order_Legend WHERE RfidCard = @RfidCard AND Step BETWEEN 1 AND 4 ORDER BY Id DESC;",
    new { RfidCard = slv },
    cancellationToken: ct));

            _log.LogInformation("PabUnified: Step={step} SLV={slv} Device={deviceId}", step, slv, deviceId);

            if (step == 1)
            {
                var entryHandler = scope.ServiceProvider.GetRequiredService<PabEntryInboundHandler>();
                await entryHandler.HandleAsync(payload, ct);
            }
            else if (step > 1 && step < 5)
            {
                var exitHandler = scope.ServiceProvider.GetRequiredService<PabExitInboundHandler>();
                await exitHandler.HandleAsync(payload, ct);
            }
            else
            {
                _log.LogWarning("PabUnified: No handler for Step={step} SLV={slv}", step, slv);
            }
        }

           
            private static string? TryExtractSlv(object payload)
            {
                if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
                {
                    if (el.TryGetProperty("carteSlv", out var v1) && v1.ValueKind == JsonValueKind.String) return v1.GetString();
                    if (el.TryGetProperty("slv", out var v2) && v2.ValueKind == JsonValueKind.String) return v2.GetString();
                }
                return payload.GetType().GetProperty("carteSlv")?.GetValue(payload)?.ToString()
                    ?? payload.GetType().GetProperty("slv")?.GetValue(payload)?.ToString();
            }

            private static string? TryExtractDeviceId(object payload)
            {
                if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
                    if (el.TryGetProperty("deviceId", out var v) && v.ValueKind == JsonValueKind.String)
                        return v.GetString();

                return payload.GetType().GetProperty("deviceId")?.GetValue(payload)?.ToString();
            }

       
    }
    }
