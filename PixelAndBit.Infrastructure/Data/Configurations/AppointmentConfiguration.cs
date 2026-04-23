using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PixelAndBit.Domain.Entities;

namespace PixelAndBit.Infrastructure.Data.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ConfirmationCode)
               .IsRequired()
               .HasMaxLength(20);

        builder.HasIndex(a => a.ConfirmationCode)
               .IsUnique();

        builder.Property(a => a.CustomerName).IsRequired().HasMaxLength(150);
        builder.Property(a => a.CustomerEmail).IsRequired().HasMaxLength(200);
        builder.Property(a => a.CustomerPhone).HasMaxLength(30);

        builder.Property(a => a.DeviceDescription)
               .IsRequired()
               .HasMaxLength(500);

        builder.Property(a => a.Status)
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.Property(a => a.Notes)
               .HasMaxLength(1000);

        builder.Property(a => a.CreatedAt)
               .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(a => a.RepairService)
               .WithMany(rs => rs.Appointments)
               .HasForeignKey(a => a.RepairServiceId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}