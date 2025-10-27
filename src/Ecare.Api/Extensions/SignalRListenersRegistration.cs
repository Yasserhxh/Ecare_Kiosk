using Ecare.Application.Services;
using Ecare.Application.Services.Ecare.Application.Services;
using Ecare.Application.Services.Handlers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecare.Api.Extensions
{
    public static class SignalRListenersRegistration
    {
        public static IServiceCollection AddSignalRListeners(this IServiceCollection services, IConfiguration cfg)
        {
            // Validate configuration
            var entrySection = cfg.GetSection("SignalRInbound");
            var exitSection = cfg.GetSection("SignalRInboundExit");

            if (!entrySection.Exists())
                throw new InvalidOperationException("Missing 'SignalRInbound' section");
            if (!exitSection.Exists())
                throw new InvalidOperationException("Missing 'SignalRInboundExit' section");

            // HTTP client
            services.AddHttpClient(nameof(SignalRHubListener));

            // Outbound configs
            services.Configure<PabEntryOutboundOptions>(cfg.GetSection("PabEntryOutbound"));
            services.Configure<PabExitOutboundOptions>(cfg.GetSection("PabExitOutbound"));

            // Named options
            services.Configure<SignalRListenerOptions>("PabEntry", entrySection);
            services.Configure<SignalRListenerOptions>("PabExit", exitSection);

            // Handlers
            services.AddSingleton<PabEntryInboundHandler>();
            services.AddSingleton<PabExitInboundHandler>();

            // Register BOTH listeners as hosted services
            services.AddHostedService<PabEntryListener>();
            services.AddHostedService<PabExitListener>();

            return services;
        }
    }
}