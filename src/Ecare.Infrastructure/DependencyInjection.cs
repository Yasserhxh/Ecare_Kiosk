// Ecare.Infrastructure/DependencyInjection.cs
using Ecare.Domain.Interfaces;                         // ISignalRNegotiator
using Ecare.Infrastructure.Persistence;               // EcareDbContext, IDbConnectionFactory, SqlConnectionFactory, DapperUnitOfWork
using Ecare.Infrastructure.Printing;                  // IBlPrinter, MockBlPrinter
using Ecare.Infrastructure.Repositories;              // *Repository impls
using Ecare.Infrastructure.Services.AzureSignalR;     // AzureSignalRNegotiator, SignalRInfraOptions
using Ecare.Infrastructure.Services.SignalR;
using Ecare.Shared;
using Microsoft.Azure.SignalR.Management;             // ServiceManager, ServiceHubContext
using Microsoft.EntityFrameworkCore;                  // UseSqlServer
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Ecare.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// One-stop call from Program.cs.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddPersistence(cfg);
        services.AddRepositories();
        services.AddPrinting();
        services.AddSignalRNegotiationSafe(cfg);

        services.Configure<ScanEventsOptions>(cfg.GetSection("ScanEvents"));// safe: falls back to Noop if missing
        return services;
    }

    // --------------------------- Persistence ---------------------------

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration cfg)
    {
        var sqlCs = cfg.GetConnectionString("SqlServer");

        services.AddDbContext<EcareDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(sqlCs))
            {
                // NOTE: Use InMemory only to allow the app to boot without SQL configured.
                // If you prefer to fail hard, replace with a throw.
                
            }
            else
            {
                options.UseSqlServer(sqlCs, b => b.MigrationsAssembly(typeof(EcareDbContext).Assembly.FullName));
            }
        });

        services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(sqlCs ?? string.Empty));
        services.AddScoped<IUnitOfWork, DapperUnitOfWork>();

        return services;
    }

    // --------------------------- Repositories ---------------------------

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IWeighRepository, WeighRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IOrderItemRepository, OrderItemRepository>();
        services.AddScoped<IEcareCimentRepository, EcareCimentRepository>();
        services.AddScoped<IClientEquipementRepository, ClientEquipementRepository>();
        services.AddScoped<IKioskDriverRepository, KioskDriverRepository>();
        services.AddScoped<IKioskOrderRepository, KioskOrderRepository>();
        services.AddScoped<ILegacyOrderWriter, LegacyOrderWriter>();

       
        services.AddSingleton<ISlvScanPublisher, SlvScanPublisher>();
        return services;
    }

    // --------------------------- Printing ---------------------------

    public static IServiceCollection AddPrinting(this IServiceCollection services)
    {
        services.AddSingleton<IBlPrinter, MockBlPrinter>();
        return services;
    }

    // --------------------------- SignalR Negotiation ---------------------------

    /// <summary>
    /// Registers Azure SignalR negotiator if configured; otherwise registers a harmless no-op.
    /// This prevents startup failures in environments where SignalR isn't configured yet.
    /// </summary>
    public static IServiceCollection AddSignalRNegotiationSafe(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SignalRInfraOptions>(configuration.GetSection("SignalR"));

        // Peek the connection string first to decide which path to take
        var connectionString = configuration["SignalR:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // No SignalR config -> register Noop negotiator so DI still resolves ISignalRNegotiator
            services.AddSingleton<ISignalRNegotiator, NoopNegotiator>();
            return services;
        }

        // With a valid connection string: wire real Azure SignalR negotiator
        services.AddSingleton<ServiceManager>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<SignalRInfraOptions>>().Value;
            // opt.ConnectionString is expected to be non-empty here
            return new ServiceManagerBuilder()
                .WithOptions(o => o.ConnectionString = opt.ConnectionString!)
                .BuildServiceManager();
        });

        // shared hub contexts cache
        services.AddSingleton(new ConcurrentDictionary<string, ServiceHubContext>());

        services.AddSingleton<ISignalRNegotiator, AzureSignalRNegotiator>();
        return services;
    }

    /// <summary>
    /// If you ever want the strict behavior back (throw when missing), keep this version.
    /// </summary>
    public static IServiceCollection AddSignalRNegotiation(this IServiceCollection services, IConfiguration configuration, bool throwIfMissing)
    {
        services.Configure<SignalRInfraOptions>(configuration.GetSection("SignalR"));

        services.AddSingleton<ServiceManager>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<SignalRInfraOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opt.ConnectionString) && throwIfMissing)
                throw new InvalidOperationException("SignalR:ConnectionString missing in configuration.");
            return new ServiceManagerBuilder()
                .WithOptions(o => o.ConnectionString = opt.ConnectionString ?? string.Empty)
                .BuildServiceManager();
        });

        services.AddSingleton(new ConcurrentDictionary<string, ServiceHubContext>());
        services.AddSingleton<ISignalRNegotiator, AzureSignalRNegotiator>();
        return services;
    }

    // --------------------------- Internal fallback ---------------------------

    private sealed class NoopNegotiator : ISignalRNegotiator
    {
        public Task<Domain.ValueObjects.NegotiateResult> NegotiateAsync(Domain.ValueObjects.NegotiateRequest request, CancellationToken ct = default)
            => Task.FromResult(new Domain.ValueObjects.NegotiateResult
            {
                Url = string.Empty,
                AccessToken = string.Empty,
                Hub = request.HubName ?? "slv_hub"
            });
    }
}
