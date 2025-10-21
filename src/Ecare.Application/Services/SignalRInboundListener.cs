using global::Ecare.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using Ecare.Infrastructure.Services.AzureSignalR; // for SignalRInfraOptions
using Ecare.Domain.Interfaces;                   // for ISignalRNegotiator
      // for RfidEnvelope (DTO)

namespace Ecare.Application.Services;

public sealed class SignalRInboundListener : BackgroundService
{
    private readonly ILogger<SignalRInboundListener> _log;
    private readonly SignalRInfraOptions _opt;
    private readonly IMediator _mediator;
    private readonly ISignalRNegotiator _negotiator;

    private HubConnection? _conn;
    private string? _endpoint; // stable endpoint to use for .WithUrl(...)

    public SignalRInboundListener(
        ILogger<SignalRInboundListener> log,
        IOptions<SignalRInfraOptions> opt,
        IMediator mediator,
        ISignalRNegotiator negotiator)
    {
        _log = log;
        _opt = opt.Value;
        _mediator = mediator;
        _negotiator = negotiator;

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
                // Get initial URL + token from Infra negotiator
                var first = await _negotiator.NegotiateAsync(stoppingToken);
                _endpoint = first.Url; // keep URL stable

                _conn = new HubConnectionBuilder()
                    .WithUrl(_endpoint, options =>
                    {
                        // On every (re)connect, ask Infra to mint a fresh token
                        options.AccessTokenProvider = async () =>
                        {
                            var p = await _negotiator.NegotiateAsync(stoppingToken);
                            return p.AccessToken;
                        };
                    })
                    .WithAutomaticReconnect()
                    .Build();

                // Handle inbound messages: { carteSlv, tsUtc }
                _conn.On<RfidEnvelope>(_opt.MethodName, async msg =>
                {
                    var slv = msg.CarteSlv;
                    if (string.IsNullOrWhiteSpace(slv))
                    {
                        _log.LogWarning("Received {Method} without carteSlv", _opt.MethodName);
                        return;
                    }

                    _log.LogInformation("Inbound RFID event: CarteSLV={slv}", slv);
                    try
                    {
                        var result = await _mediator.Send(new ScanBySlvQuery(slv), stoppingToken);
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
            catch (OperationCanceledException) { /* shutdown */ }
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
