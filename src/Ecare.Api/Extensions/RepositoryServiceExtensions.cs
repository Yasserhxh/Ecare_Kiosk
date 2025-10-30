using Ecare.Infrastructure.Repositories;

namespace Ecare.Api.Extensions;

public static class RepositoryServiceExtensions
{
    /// <summary>
    /// Automatically registers all repository implementations from the Infrastructure assembly.
    /// Scans for classes ending with "Repository" or "Writer" and registers them with their interfaces.
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        var assembly = typeof(IOrderRepository).Assembly; // Infrastructure assembly

        var repositoryTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                   (t.Name.EndsWith("Repository") || t.Name.EndsWith("Writer")))
            .ToList();

        foreach (var implementationType in repositoryTypes)
        {
            var interfaces = implementationType.GetInterfaces()
                .Where(i => i.Name.EndsWith("Repository") || i.Name.EndsWith("Writer"))
                .ToList();

            foreach (var interfaceType in interfaces)
            {
                services.AddScoped(interfaceType, implementationType);
            }
        }

        return services;
    }

    /// <summary>
    /// Registers infrastructure services (printers, etc.)
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        var assembly = typeof(IOrderRepository).Assembly;

        // Register all printer implementations as singletons
        var printerTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Printer"))
            .ToList();

        foreach (var implementationType in printerTypes)
        {
            var interfaces = implementationType.GetInterfaces()
                .Where(i => i.Name.EndsWith("Printer"))
                .ToList();

            foreach (var interfaceType in interfaces)
            {
                services.AddSingleton(interfaceType, implementationType);
            }
        }

        return services;
    }
}