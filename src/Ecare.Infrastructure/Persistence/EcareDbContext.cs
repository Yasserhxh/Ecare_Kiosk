using Ecare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecare.Infrastructure.Persistence;

public class EcareDbContext(DbContextOptions<EcareDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Line> Lines => Set<Line>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<WeighRecord> Weighings => Set<WeighRecord>();
    public DbSet<BonLivraison> BonLivraisons => Set<BonLivraison>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(EcareDbContext).Assembly);
}
