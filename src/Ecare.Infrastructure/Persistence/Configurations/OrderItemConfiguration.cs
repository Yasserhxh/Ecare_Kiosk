using Ecare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecare.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("Ecare_OrderItems", schema: "dbo");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("Id");

        builder.Property(item => item.OrderId)
            .HasColumnName("OrderId")
            .IsRequired();

        builder.Property(item => item.ProductId)
            .HasColumnName("ProductId")
            .IsRequired();

        builder.Property(item => item.Quantity)
            .HasColumnName("Quantity")
            .IsRequired();

        builder.Property(item => item.Unite)
            .HasColumnName("Unite");
    }
}
