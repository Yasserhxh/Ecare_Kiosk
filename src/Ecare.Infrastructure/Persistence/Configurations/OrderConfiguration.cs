using Ecare.Domain;
using Ecare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecare.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", schema: "dbo");

        builder.HasKey(o => o.Id);

        builder.HasIndex(o => o.Number)
            .IsUnique();

        builder.Property(o => o.Id)
            .HasColumnName("Id");

        builder.Property(o => o.ShippingId)
            .HasColumnName("ShippingId")
            .IsRequired(false);

        builder.Property(o => o.Number)
            .HasColumnName("NumeroCommande")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(o => o.OrderedAt)
            .HasColumnName("DateCommande")
            .IsRequired(false);

        builder.Property(o => o.Destination)
            .HasColumnName("Destination")
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(o => o.DeliveryMode)
            .HasColumnName("ModeDelivraison")
            .HasMaxLength(120)
            .IsRequired(false);

        builder.Property(o => o.LoadingLocation)
            .HasColumnName("EmplacementChargement")
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(o => o.LoadingMethod)
            .HasColumnName("MethodeChargement")
            .HasMaxLength(120)
            .IsRequired(false);

        builder.Property(o => o.PickupTime)
            .HasColumnName("HeureEnlevement")
            .IsRequired(false);

        builder.Property(o => o.DeliveryTime)
            .HasColumnName("HeureDelivraison")
            .IsRequired(false);

        builder.Property(o => o.SlvCard)
            .HasColumnName("CarteSLV")
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(o => o.DriverLastName)
            .HasColumnName("ChauffeurNom")
            .HasMaxLength(120)
            .IsRequired(false);

        builder.Property(o => o.DriverFirstName)
            .HasColumnName("ChauffeurPrenom")
            .HasMaxLength(120)
            .IsRequired(false);

        builder.Property(o => o.DriverLicense)
            .HasColumnName("PermisDeConduire")
            .HasMaxLength(80)
            .IsRequired(false);

        builder.Property(o => o.TruckPlate)
            .HasColumnName("PlaqueCamion")
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(o => o.Status)
            .HasColumnName("Statut")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(o => o.UserId)
            .HasColumnName("UserId")
            .IsRequired(false);

        builder.Ignore(o => o.TargetQuantityTons);
    }
}
