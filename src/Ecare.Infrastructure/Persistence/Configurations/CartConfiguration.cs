using Ecare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecare.Infrastructure.Persistence.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts", schema: "dbo");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("Id");

        builder.Property(c => c.ProduitId)
            .HasColumnName("ProduitId")
            .IsRequired();

        builder.Property(c => c.NomProduit)
            .HasColumnName("NomProduit")
            .IsRequired();

        builder.Property(c => c.Quantite)
            .HasColumnName("Quantite")
            .IsRequired();

        builder.Property(c => c.Unite)
            .HasColumnName("Unite");

        builder.Property(c => c.DateAjout)
            .HasColumnName("DateAjout")
            .IsRequired();

        builder.Property(c => c.UserId)
            .HasColumnName("UserId")
            .IsRequired();

        builder.Property(c => c.ShippingId)
            .HasColumnName("ShippingId");
    }
}
