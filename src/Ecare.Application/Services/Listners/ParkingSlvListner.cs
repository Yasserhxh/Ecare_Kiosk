using Ecare.Application.Services.Ecare.Application.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecare.Application.Services;

public sealed class ParkingSlvListener : BackgroundService
{
    private readonly SignalRHubListener _inner;

    public ParkingSlvListener(
        ILogger<SignalRHubListener> log,
        IHttpClientFactory http,
        IOptionsMonitor<SignalRListenerOptions> options,
        ParkingSlvInboundHandler handler)
    {
        var opt = options.Get("Parking");
        log.LogInformation("Initializing ParkingSlvListener: Hub={Hub}, Method={Method}",
            opt.Hub, opt.Method);

        _inner = new SignalRHubListener(log, http, Options.Create(opt), handler);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => ((IHostedService)_inner).StartAsync(stoppingToken);

    public override Task StopAsync(CancellationToken token)
        => _inner.StopAsync(token);
}
