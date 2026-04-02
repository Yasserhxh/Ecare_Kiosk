using Ecare.Application.Services;
using Ecare.Application.Services.Handlers;

namespace Ecare.Api.Extensions
{
    public static class SignalRListenersRegistration
    {
        public static IServiceCollection AddSignalRListeners(this IServiceCollection services, IConfiguration cfg)
        {
            // Validate configuration
            var entrySection = cfg.GetSection("SignalRInbound");
            var exitSection = cfg.GetSection("SignalRInboundExit");
            var loadingSection = cfg.GetSection("SignalRInboundLoading");
            var parkingSection = cfg.GetSection("SignalRInboundParking");

            if (!entrySection.Exists())
                throw new InvalidOperationException("Missing 'SignalRInbound' section");
            if (!exitSection.Exists())
                throw new InvalidOperationException("Missing 'SignalRInboundExit' section");
            if (!loadingSection.Exists())
                throw new InvalidOperationException("Missing 'SignalRInboundLoading' section");
            if (!loadingSection.Exists())
                throw new InvalidOperationException("Missing 'SignalRInboundParking' section");

            // HTTP client
            services.AddHttpClient(nameof(SignalRHubListener));
            services.AddHttpClient("kiosk");


            // Outbound configs
            services.Configure<PabEntryOutboundOptions>(cfg.GetSection("PabEntryOutbound"));
            services.Configure<PabExitOutboundOptions>(cfg.GetSection("PabExitOutbound"));
            services.Configure<LoadingOutboundOptions>(cfg.GetSection("LoadingOutbound"));
            services.Configure<ParkingOutboundOptions>(cfg.GetSection("ParkingOutbound"));

            // Named options
            services.Configure<SignalRListenerOptions>("PabEntry", entrySection);
            services.Configure<SignalRListenerOptions>("PabExit", exitSection);
            services.Configure<SignalRListenerOptions>("Loading", loadingSection);
            services.Configure<SignalRListenerOptions>("Parking", parkingSection);

            // Handlers
            services.AddSingleton<PabEntryInboundHandler>();
            services.AddSingleton<PabExitInboundHandler>();
            services.AddSingleton<LoadingInboundHandler>();
            services.AddSingleton<ParkingSlvInboundHandler>();

            // Register BOTH listeners as hosted services
            services.AddHostedService<PabEntryListener>();
            services.AddHostedService<PabExitListener>();
            services.AddHostedService<LoadingListner>();
            services.AddHostedService<ParkingSlvListner>();
            //services.AddHostedService<FluxRealtimeTicker>();


            return services;
        }
    }
}