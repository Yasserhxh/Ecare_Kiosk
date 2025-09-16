using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Ecare.Infrastructure.Persistence;

public sealed class EcareDbContextFactory : IDesignTimeDbContextFactory<EcareDbContext>
{
    public EcareDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("SqlServer");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var apiBasePath = Path.Combine(basePath, "..", "Ecare.Api");
            configuration = new ConfigurationBuilder()
                .SetBasePath(apiBasePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            connectionString = configuration.GetConnectionString("SqlServer");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Could not find a connection string named 'SqlServer'.");
        }

        var options = new DbContextOptionsBuilder<EcareDbContext>()
          .UseSqlServer(connectionString,
              b => b.MigrationsAssembly(typeof(EcareDbContext).Assembly.FullName))
          .Options;

        return new EcareDbContext(options);
    }
}
