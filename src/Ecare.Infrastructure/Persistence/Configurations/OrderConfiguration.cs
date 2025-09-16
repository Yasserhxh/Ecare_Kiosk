using Ecare.Domain;
using Ecare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecare.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);

        builder.HasIndex(o => o.Number)
            .IsUnique();

        builder.Property(o => o.Number)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(o => o.ProductId)
            .IsRequired();

        builder.Property(o => o.ProductType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(o => o.ProductName)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(o => o.Unit)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.Quantity)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<int>()
            .HasDefaultValue(OrderStatus.Cree)
            .IsRequired();

        builder.Property(o => o.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(o => o.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(o => o.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
