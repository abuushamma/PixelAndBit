using System.ComponentModel.DataAnnotations;

namespace PixelAndBit.Web.Models;

public class AdminProductEditVm
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 1_000_000)]
    public decimal Price { get; set; }

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(0, 1_000_000)]
    public int StockQuantity { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }
}

