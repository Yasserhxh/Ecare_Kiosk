using Ecare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecare.Infrastructure.Persistence.Configurations;

public sealed class ShippingConfiguration : IEntityTypeConfiguration<Shipping>
{
    public void Configure(EntityTypeBuilder<Shipping> builder)
    {
        builder.ToTable("Shippings", schema: "dbo");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("Id");

        builder.Property(s => s.UserId)
            .HasColumnName("UserId")
            .IsRequired();

        builder.Property(s => s.Destination)
            .HasColumnName("Destination");

        builder.Property(s => s.DeliveryMode)
            .HasColumnName("ModeDelivraison");

        builder.Property(s => s.DeliveryTime)
            .HasColumnName("HeureDelivraison");

        builder.Property(s => s.NumeroCommande)
            .HasColumnName("NumeroCommande");

        builder.Property(s => s.LoadingMethod)
            .HasColumnName("MethodeChargement");

        builder.Property(s => s.LoadingLocation)
            .HasColumnName("EmplacementChargement");

        builder.Property(s => s.PickupTime)
            .HasColumnName("HeureEnlevement");

        builder.Property(s => s.SlvCard)
            .HasColumnName("CarteSLV");

        builder.Property(s => s.DriverId)
            .HasColumnName("ChauffeurId");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("DateCreation")
            .IsRequired();

        builder.Property(s => s.Commentaire)
            .HasColumnName("Commentaire");
    }
}
