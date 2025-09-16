using Ecare.Domain.Entities;
using Ecare.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecare.Infrastructure.Persistence.Configurations;

public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Slv)
            .HasConversion(
                v => v.Value,
                v => SlvId.From(v))
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(d => d.Slv)
            .IsUnique();

        builder.Property(d => d.FirstName)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(d => d.LastName)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(d => d.Plate)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(d => d.ClientId)
            .IsRequired(false);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(d => d.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
