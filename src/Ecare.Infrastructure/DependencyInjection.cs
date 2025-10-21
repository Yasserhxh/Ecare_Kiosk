using Ecare.Domain.Interfaces;
using Ecare.Infrastructure.Services.AzureSignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        // other services...

        services.Configure<SignalRInfraOptions>(cfg.GetSection("SignalR"));
        services.AddSingleton<ISignalRNegotiator, AzureSignalRNegotiator>();

        return services;
    }
}