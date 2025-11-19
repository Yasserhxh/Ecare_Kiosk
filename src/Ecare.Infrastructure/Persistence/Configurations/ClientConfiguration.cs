using Ecare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Client", schema: "dbo");

        builder.HasKey(c => c.Client_Id);

        builder.Property(c => c.Client_Id)
            .HasColumnName("Client_Id");

        // Do NOT map computed property “Name”
        builder.Ignore(c => c.Name);
        builder.Ignore(c => c.SapCode);
        builder.Ignore(c => c.SapOk);

        // Map actual persisted columns instead
        builder.Property(c => c.Nom_Complet)
            .HasMaxLength(200)
            .HasColumnName("Nom_Complet")
            .IsRequired(false);

        builder.Property(c => c.CodeClientSap)
            .HasMaxLength(50)
            .HasColumnName("CodeClientSap")
            .IsRequired(false);
    }
}
