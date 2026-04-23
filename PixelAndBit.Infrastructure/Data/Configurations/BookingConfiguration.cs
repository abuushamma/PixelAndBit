using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PixelAndBit.Domain.Entities;

namespace PixelAndBit.Infrastructure.Data.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.TicketReference)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(b => b.TicketReference)
            .IsUnique();

        builder.Property(b => b.CustomerName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(b => b.UserId)
            .HasMaxLength(450);

        builder.Property(b => b.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(b => b.DeviceModel)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.IssueDescription)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(b => b.EstimatedCost)
            .HasColumnType("decimal(10, 2)");

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(30);
    }
}

