using global::Ecare.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;   // <-- for IServiceScopeFactory / CreateScope()
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using Ecare.Infrastructure.Services.AzureSignalR;
using Ecare.Domain.Interfaces;
using Ecare.Application.Services;

namespace Ecare.Application.Services;

public sealed class SignalRInboundListener : BackgroundService
{
    private readonly ILogger<SignalRInboundListener> _log;
    private readonly SignalRInfraOptions _opt;
    private readonly ISignalRNegotiator _negotiator;
    private readonly IServiceScopeFactory _scopeFactory;   // <-- add

    private HubConnection? _conn;
    private string? _endpoint;

    public SignalRInboundListener(
        ILogger<SignalRInboundListener> log,
        IOptions<SignalRInfraOptions> opt,
        ISignalRNegotiator negotiator,
        IServiceScopeFactory scopeFactory)                  // <-- add
    {
        _log = log;
        _opt = opt.Value;
        _negotiator = negotiator;
        _scopeFactory = scopeFactory;                      // <-- save

        if (string.IsNullOrWhiteSpace(_opt.HubName))
            throw new InvalidOperationException("SignalR:HubName missing");
        if (string.IsNullOrWhiteSpace(_opt.MethodName))
            throw new InvalidOperationException("SignalR:MethodName missing");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var first = await _negotiator.NegotiateAsync(stoppingToken);
                _endpoint = first.Url;

                _conn = new HubConnectionBuilder()
                    .WithUrl(_endpoint, options =>
                    {
                        options.AccessTokenProvider = async () =>
                        {
                            var p = await _negotiator.NegotiateAsync(stoppingToken);
                            return p.AccessToken;
                        };
                    })
                    .WithAutomaticReconnect()
                    .Build();

                _conn.On<RfidEnvelope>(_opt.MethodName, async msg =>
                {
                    var slv = msg.CarteSlv;
                    if (string.IsNullOrWhiteSpace(slv))
                    {
                        _log.LogWarning("Received {Method} without carteSlv", _opt.MethodName);
                        return;
                    }

                    _log.LogInformation("Inbound RFID event: CarteSLV={slv}", slv);

                    // 🔑 Create a scope per message, resolve scoped services (IMediator -> handlers -> repositories/DbContext)
                    using var scope = _scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    try
                    {
                        var result = await mediator.Send(new ScanBySlvQuery(slv), stoppingToken);
                        if (!result.Success)
                            _log.LogWarning("ScanBySlvQuery failed: {err}", result.Error);
                        else
                            _log.LogInformation("ScanBySlv ok: DriverId={id} Plate={plate}",
                                result.Value.DriverId, result.Value.Plate);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "ScanBySlv handler threw");
                    }
                });

                await _conn.StartAsync(stoppingToken);
                _log.LogInformation("SignalR inbound connected to hub {Hub}", _opt.HubName);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) { /* shutting down */ }
            catch (Exception ex)
            {
                _log.LogError(ex, "Inbound listener error; retrying in 3s");
                try { await Task.Delay(3000, stoppingToken); } catch { }
            }
            finally
            {
                if (_conn is not null)
                {
                    try { await _conn.DisposeAsync(); } catch { }
                    _conn = null;
                }
            }
        }
    }
}
