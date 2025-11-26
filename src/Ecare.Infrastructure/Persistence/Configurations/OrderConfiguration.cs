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
        builder.HasIndex(o => o.NumeroCommande).IsUnique();

        builder.Property(o => o.Id).HasColumnName("Id");
        builder.Property(o => o.ShippingId).HasColumnName("ShippingId").IsRequired(false);
        builder.Property(o => o.NumeroCommande).HasColumnName("NumeroCommande").HasMaxLength(60).IsRequired();
        builder.Property(o => o.DateCommande).HasColumnName("DateCommande").IsRequired(false);
        builder.Property(o => o.Destination).HasColumnName("Destination").HasMaxLength(255).IsRequired(false);
        builder.Property(o => o.ModeDelivraison).HasColumnName("ModeDelivraison").HasMaxLength(120).IsRequired(false);
        builder.Property(o => o.EmplacementChargement).HasColumnName("EmplacementChargement").HasMaxLength(255).IsRequired(false);
        builder.Property(o => o.MethodeChargement).HasColumnName("MethodeChargement").HasMaxLength(120).IsRequired(false);
        builder.Property(o => o.HeureEnlevement).HasColumnName("HeureEnlevement").IsRequired(false);
        builder.Property(o => o.HeureDelivraison).HasColumnName("HeureDelivraison").IsRequired(false);
        builder.Property(o => o.CarteSLV).HasColumnName("CarteSLV").HasMaxLength(50).IsRequired(false);
        builder.Property(o => o.ChauffeurNom).HasColumnName("ChauffeurNom").HasMaxLength(120).IsRequired(false);
        builder.Property(o => o.ChauffeurPrenom).HasColumnName("ChauffeurPrenom").HasMaxLength(120).IsRequired(false);
        builder.Property(o => o.PermisDeConduire).HasColumnName("PermisDeConduire").HasMaxLength(80).IsRequired(false);
        builder.Property(o => o.PlaqueCamion).HasColumnName("PlaqueCamion").HasMaxLength(50).IsRequired(false);
        builder.Property(o => o.Statut).HasColumnName("Statut").HasMaxLength(50).IsRequired();
        builder.Property(o => o.UserId).HasColumnName("UserId").IsRequired(false);
        builder.Property(o => o.Commentaire).HasColumnName("Commentaire").HasMaxLength(255).IsRequired(false);
        builder.Property(o => o.NomComplet).HasColumnName("NomComplet").HasMaxLength(255).IsRequired(false);

        //Ignore all computed (read-only) properties
        builder.Ignore(o => o.Number);
        builder.Ignore(o => o.OrderedAt);
        builder.Ignore(o => o.DeliveryMode);
        builder.Ignore(o => o.LoadingLocation);
        builder.Ignore(o => o.LoadingMethod);
        builder.Ignore(o => o.PickupTime);
        builder.Ignore(o => o.DeliveryTime);
        builder.Ignore(o => o.SlvCard);
        builder.Ignore(o => o.DriverNom);
        builder.Ignore(o => o.DriverPrenom);
        builder.Ignore(o => o.DriverLicense);
        builder.Ignore(o => o.TruckPlate);
        builder.Ignore(o => o.Status);
        builder.Ignore(o => o.TargetQuantityTons);
    }
}
