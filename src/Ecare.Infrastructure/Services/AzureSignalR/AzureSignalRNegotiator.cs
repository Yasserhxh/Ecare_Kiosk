using System.Security.Claims;
using Ecare.Domain.Interfaces;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Options;

namespace Ecare.Infrastructure.Services.AzureSignalR;

public sealed class SignalRInfraOptions
{
    public string ConnectionString { get; set; } = default!;
    public string HubName { get; set; } = default!;
    public string MethodName { get; set; } = "ReceiveRfid";
}

public sealed class AzureSignalRNegotiator : ISignalRNegotiator
{
    private readonly IServiceManager _serviceManager;
    private readonly SignalRInfraOptions _options;

    public AzureSignalRNegotiator(IOptions<SignalRInfraOptions> options)
    {
        _options = options.Value;

        // Build IServiceManager (note: BuildServiceManager, NOT Build)
        _serviceManager = (IServiceManager?)new ServiceManagerBuilder()
            .WithOptions(o => o.ConnectionString = _options.ConnectionString)
            .BuildServiceManager();
    }

    public Task<NegotiatePayload> NegotiateAsync(CancellationToken ct = default)
    {
        // Optional: add userId or claims if you need to target users/groups later
        string url = _serviceManager.GetClientEndpoint(_options.HubName);
        string token = _serviceManager.GenerateClientAccessToken(
            _options.HubName,
            userId: null,                  // or a real user id
            claims: Array.Empty<Claim>(),  // add claims if needed
            lifeTime: TimeSpan.FromHours(1)
        );

        return Task.FromResult(new NegotiatePayload(url, token));
    }
}