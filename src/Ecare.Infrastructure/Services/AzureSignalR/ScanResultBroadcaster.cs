using Ecare.Domain.Contracts;
using Ecare.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Infrastructure.Services.AzureSignalR
{


    public sealed class ScanResultBroadcaster : IScanResultBroadcaster, IAsyncDisposable
    {
        private readonly ILogger<ScanResultBroadcaster> _log;
        private readonly IServiceManager _mgr;
        private readonly SignalRInfraOptions _opt;
        private ServiceHubContext? _hub;
        private const string MethodName = "ReceiveScanResult";

        public ScanResultBroadcaster(
            ILogger<ScanResultBroadcaster> log,
            IOptions<SignalRInfraOptions> opt)
        {
            _log = log;
            _opt = opt.Value;

            if (string.IsNullOrWhiteSpace(_opt.ConnectionString))
                throw new InvalidOperationException("SignalR2:ConnectionString missing");
            if (string.IsNullOrWhiteSpace("data_hub"))
                throw new InvalidOperationException("SignalR:HubName missing");

            _mgr = (IServiceManager?)new ServiceManagerBuilder()
                .WithOptions(o => o.ConnectionString = _opt.ConnectionString)
                .BuildServiceManager();
        }

        private async Task<ServiceHubContext> EnsureHubAsync(CancellationToken ct)
        {
            if (_hub != null) return _hub;

            _hub = (ServiceHubContext?)await _mgr.CreateHubContextAsync(
                "data_hub",
                Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance // avoid the ILoggerFactory/CancellationToken mismatch
            );

            return _hub!;
        }


        public async Task BroadcastAsync(ScanResultMessage message, CancellationToken ct = default)
        {
            try
            {
                var hub = await EnsureHubAsync(ct);
                await hub.Clients.All.SendAsync(MethodName, message, ct);
                _log.LogInformation("Broadcasted scan result for SLV={slv}", message.CarteSLV);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Broadcast failed");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_hub is not null) await _hub.DisposeAsync();
        }
    }
}