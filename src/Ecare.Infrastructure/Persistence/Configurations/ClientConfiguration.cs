using Ecare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecare.Infrastructure.Persistence.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Client", schema: "dbo");

        builder.HasKey(c => c.Client_Id);

        builder.Property(c => c.Client_Id)
            .HasColumnName("Client_Id");

        builder.Property(c => c.Name)
            .HasColumnName("Nom_Complet")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.SapCode)
            .HasColumnName("CodeClientSap")
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Ignore(c => c.SapOk);
    }
}
