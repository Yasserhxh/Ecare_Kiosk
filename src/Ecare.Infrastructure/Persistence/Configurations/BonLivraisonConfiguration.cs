using Ecare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecare.Infrastructure.Persistence.Configurations;

public sealed class BonLivraisonConfiguration : IEntityTypeConfiguration<BonLivraison>
{
    public void Configure(EntityTypeBuilder<BonLivraison> builder)
    {
        builder.ToTable("BonLivraison");

        builder.HasKey(b => b.Id);

        builder.HasIndex(b => b.BlNumber)
            .IsUnique();

        builder.Property(b => b.BlNumber)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(b => b.NetKg)
            .IsRequired();

        builder.Property(b => b.IssuedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(b => b.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
