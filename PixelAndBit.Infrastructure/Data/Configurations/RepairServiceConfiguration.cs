using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PixelAndBit.Domain.Entities;

namespace PixelAndBit.Infrastructure.Data.Configurations;

public class RepairServiceConfiguration : IEntityTypeConfiguration<RepairService>
{
    public void Configure(EntityTypeBuilder<RepairService> builder)
    {
        builder.ToTable("RepairServices");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired();

        builder.Property(r => r.Description)
            .IsRequired();

        builder.Property(r => r.BasePrice)
            .HasColumnType("decimal(18, 2)");
    }
}
