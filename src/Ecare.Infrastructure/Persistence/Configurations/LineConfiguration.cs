using Ecare.Domain;
using Ecare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecare.Infrastructure.Persistence.Configurations;

public sealed class LineConfiguration : IEntityTypeConfiguration<Line>
{
    public void Configure(EntityTypeBuilder<Line> builder)
    {
        builder.ToTable("Lines");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(l => l.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(l => l.Capacity)
            .IsRequired();

        builder.Property(l => l.Occupied)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(l => l.Status)
            .HasConversion<int>()
            .IsRequired();
    }
}
