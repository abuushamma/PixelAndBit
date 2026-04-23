using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using PixelAndBit.Application.Interfaces;
using PixelAndBit.Domain.Entities;

namespace PixelAndBit.Infrastructure.Data;

public class CartService : ICartService
{
    private const string SessionKey = "pb_cart_v1";
    private readonly IHttpContextAccessor _http;
    private readonly PixelBitDbContext _db;

    public CartService(IHttpContextAccessor http, PixelBitDbContext db)
    {
        _http = http;
        _db = db;
    }

    public async Task AddAsync(int productId, int quantity = 1)
    {
        if (quantity <= 0) return;
        var cart = GetCart();
        cart[productId] = cart.TryGetValue(productId, out var q) ? (q + quantity) : quantity;
        SaveCart(cart);
        await Task.CompletedTask;
    }

    public async Task RemoveAsync(int productId, int quantity = 1)
    {
        if (quantity <= 0) return;
        var cart = GetCart();
        if (!cart.TryGetValue(productId, out var q)) return;
        var next = q - quantity;
        if (next <= 0) cart.Remove(productId);
        else cart[productId] = next;
        SaveCart(cart);
        await Task.CompletedTask;
    }

    public async Task ClearAsync()
    {
        var session = GetSession();
        session.Remove(SessionKey);
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<CartLine>> GetLinesAsync()
    {
        var cart = GetCart();
        if (cart.Count == 0) return Array.Empty<CartLine>();

        var ids = cart.Keys.ToArray();
        var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToListAsync();
        var byId = products.ToDictionary(p => p.Id);

        var lines = new List<CartLine>();
        foreach (var (id, qty) in cart)
        {
            if (qty <= 0) continue;
            if (byId.TryGetValue(id, out var p))
                lines.Add(new CartLine(p, qty));
        }
        return lines;
    }

    public async Task<int> GetItemCountAsync()
    {
        var cart = GetCart();
        return await Task.FromResult(cart.Values.Sum());
    }

    public async Task<decimal> GetTotalAsync()
    {
        var lines = await GetLinesAsync();
        return lines.Sum(l => l.Product.Price * l.Quantity);
    }

    private ISession GetSession()
    {
        var ctx = _http.HttpContext ?? throw new InvalidOperationException("No active HttpContext.");
        return ctx.Session;
    }

    private Dictionary<int, int> GetCart()
    {
        var session = GetSession();
        var json = session.GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<int, int>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<int, int>>(json) ?? new Dictionary<int, int>();
        }
        catch
        {
            return new Dictionary<int, int>();
        }
    }

    private void SaveCart(Dictionary<int, int> cart)
    {
        var session = GetSession();
        session.SetString(SessionKey, JsonSerializer.Serialize(cart));
    }
}

