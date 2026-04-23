using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PixelAndBit.Domain.Entities;

namespace PixelAndBit.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(p => p.Description)
               .HasMaxLength(2000)
               .IsRequired();

        builder.Property(p => p.Price)
               .HasColumnType("decimal(10, 2)");  // Precise money type

        builder.Property(p => p.StockQuantity)
               .HasColumnType("int");

        builder.Property(p => p.ImageUrl)
               .HasMaxLength(500);
    }
}