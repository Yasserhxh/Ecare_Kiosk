using Ecare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecare.Infrastructure.Persistence.Configurations;

public class EcareEngineConfiguration : IEntityTypeConfiguration<EcareEngine>
{
    public void Configure(EntityTypeBuilder<EcareEngine> builder)
    {
        builder.ToTable("Ecare_Engine");

        builder.HasKey(e => e.Matricule);

        builder.Property(e => e.Matricule)
            .HasColumnName("Matricule")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(e => e.PTAC)
            .HasColumnName("PTAC");

        builder.Property(e => e.TARE)
            .HasColumnName("TARE");

        builder.Property(e => e.Type_Camion)
            .HasColumnName("Type_Camion");

        builder.Property(e => e.CarteSlv)
            .HasColumnName("CarteSlv");

        builder.Property(e => e.Type_Process)
            .HasColumnName("Type_Process")
            .HasConversion<int?>(); // enum -> int

        builder.Property(e => e.Code_Process)
            .HasColumnName("Code_Process");

        builder.Property(e => e.Nom_Process)
            .HasColumnName("Nom_Process")
            .HasMaxLength(100);

        builder.Property(e => e.Code_Chantier)
            .HasColumnName("Code_Chantier");

        builder.Property(e => e.Nom_Chantier)
            .HasColumnName("Nom_Chantier")
            .HasMaxLength(100);

        builder.Property(e => e.Id_Chauffeur)
            .HasColumnName("Id_Chauffeur");
    }
}
