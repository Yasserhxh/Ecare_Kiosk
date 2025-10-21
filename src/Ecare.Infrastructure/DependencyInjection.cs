using System.Collections.Concurrent;
using Ecare.Domain.Interfaces;                   // ISignalRNegotiator
using Ecare.Infrastructure.Services.AzureSignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ecare.Infrastructure
{
    public static class DependencyInjection
    {
        /// <summary>
        /// Registers Azure SignalR negotiation infrastructure (no warm-up).
        /// </summary>
        public static IServiceCollection AddSignalRNegotiation(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<SignalRInfraOptions>(configuration.GetSection("SignalR"));

            services.AddSingleton<ServiceManager>(sp =>
            {
                var opt = sp.GetRequiredService<IOptions<SignalRInfraOptions>>().Value;
                if (string.IsNullOrWhiteSpace(opt.ConnectionString))
                    throw new InvalidOperationException("SignalR:ConnectionString missing in configuration.");
                return new ServiceManagerBuilder()
                    .WithOptions(o => o.ConnectionString = opt.ConnectionString!)
                    .BuildServiceManager();
            });

            services.AddSingleton(new ConcurrentDictionary<string, ServiceHubContext>());

            services.AddSingleton<ISignalRNegotiator, AzureSignalRNegotiator>();

            return services;
        }

        /// <summary>
        /// Overload for programmatic configuration.
        /// </summary>
        public static IServiceCollection AddSignalRNegotiation(
            this IServiceCollection services,
            Action<SignalRInfraOptions> configure)
        {
            services.Configure(configure);

            services.AddSingleton<ServiceManager>(sp =>
            {
                var opt = sp.GetRequiredService<IOptions<SignalRInfraOptions>>().Value;
                if (string.IsNullOrWhiteSpace(opt.ConnectionString))
                    throw new InvalidOperationException("SignalR:ConnectionString missing in options.");
                return new ServiceManagerBuilder()
                    .WithOptions(o => o.ConnectionString = opt.ConnectionString!)
                    .BuildServiceManager();
            });

            services.AddSingleton(new ConcurrentDictionary<string, ServiceHubContext>());
            services.AddSingleton<ISignalRNegotiator, AzureSignalRNegotiator>();

            return services;
        }
    }
}
