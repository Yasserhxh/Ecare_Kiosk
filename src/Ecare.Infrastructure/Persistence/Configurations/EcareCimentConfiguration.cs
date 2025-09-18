using Ecare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecare.Infrastructure.Persistence.Configurations;

public sealed class EcareCimentConfiguration : IEntityTypeConfiguration<EcareCiment>
{
    public void Configure(EntityTypeBuilder<EcareCiment> builder)
    {
        builder.ToTable("EcareCiments", schema: "dbo");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("Id");

        builder.Property(c => c.Name)
            .HasColumnName("Name")
            .IsRequired();

        builder.Property(c => c.ImageUrl)
            .HasColumnName("ImageUrl");

        builder.Property(c => c.Details)
            .HasColumnName("Details");

        builder.Property(c => c.Type)
            .HasColumnName("Type");

        builder.Property(c => c.Description)
            .HasColumnName("Description");
    }
}
