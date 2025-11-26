using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Ecare.Infrastructure.Repositories; // marker type here

namespace Ecare.Api.Extensions
{
    public static class RepositoryServiceExtensions
    {
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            // ✅ use Infrastructure assembly, not Domain
            var assembly = typeof(EcareLigneRepository).Assembly;

            var candidates = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract &&
                            (t.Name.EndsWith("Repository") || t.Name.EndsWith("Writer")))
                .ToList();

            foreach (var implementationType in candidates)
            {
                // 1) Register interface mappings
                var interfaces = implementationType.GetInterfaces()
                    .Where(i => i.Name.EndsWith("Repository") || i.Name.EndsWith("Writer"))
                    .ToList();

                foreach (var interfaceType in interfaces)
                {
                    services.AddScoped(interfaceType, implementationType);
                }

                // 2) Also register the concrete type (lets you inject class directly)
                services.AddScoped(implementationType);
            }

            return services;
        }

        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            var assembly = typeof(EcareLigneRepository).Assembly; // same fix

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

                // Optional: also allow injecting concrete printers
                services.AddSingleton(implementationType);
            }



            return services;
        }
    }
}
