using PixelAndBit.Domain.Entities;

namespace PixelAndBit.Application.Interfaces;

public interface ICartService
{
    Task AddAsync(int productId, int quantity = 1);
    Task RemoveAsync(int productId, int quantity = 1);
    Task ClearAsync();

    Task<IReadOnlyList<CartLine>> GetLinesAsync();
    Task<int> GetItemCountAsync();
    Task<decimal> GetTotalAsync();
}

public record CartLine(Product Product, int Quantity);

