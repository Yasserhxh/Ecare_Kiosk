using Ecare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecare.Infrastructure.Persistence.Configurations;

public sealed class WeighRecordConfiguration : IEntityTypeConfiguration<WeighRecord>
{
    public void Configure(EntityTypeBuilder<WeighRecord> builder)
    {
        builder.ToTable("Ecare_Kiosk_Weighings");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Stage)
            .IsRequired();

        builder.Property(w => w.GrossKg)
            .IsRequired();

        builder.Property(w => w.TareKg)
            .IsRequired();

        builder.Property(w => w.NetKg)
            .IsRequired();

        builder.Property(w => w.TakenAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(w => w.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
