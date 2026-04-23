using Microsoft.EntityFrameworkCore;
using PixelAndBit.Application.Interfaces;
using PixelAndBit.Domain.Entities;

namespace PixelAndBit.Infrastructure.Data;

public class ProductService : IProductService
{
    private readonly PixelBitDbContext _context;

    public ProductService(PixelBitDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Product>> GetAllProductsAsync()
    {
        return await _context.Products
            .ToListAsync();
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id);
    }
}