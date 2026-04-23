using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PixelAndBit.Application.Interfaces;
using PixelAndBit.Domain.Entities;
using PixelAndBit.Domain.Enums;
using PixelAndBit.Infrastructure.Data;

namespace PixelAndBit.Web.Controllers;

public class CartController : Controller
{
    private readonly ICartService _cart;
    private readonly PixelBitDbContext _db;

    public CartController(ICartService cart, PixelBitDbContext db)
    {
        _cart = cart;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var lines = await _cart.GetLinesAsync();
        var total = await _cart.GetTotalAsync();
        ViewBag.Total = total;
        return View(lines);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add([FromBody] AddToCartRequest? req)
    {
        if (req is null) return BadRequest();
        if (req.Quantity <= 0) req = req with { Quantity = 1 };

        var exists = await _db.Products.AnyAsync(p => p.Id == req.ProductId);
        if (!exists) return NotFound();

        await _cart.AddAsync(req.ProductId, req.Quantity);
        var count = await _cart.GetItemCountAsync();
        var total = await _cart.GetTotalAsync();
        return Ok(new { count, total });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        await _cart.ClearAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm()
    {
        var lines = await _cart.GetLinesAsync();
        if (lines.Count == 0) return RedirectToAction(nameof(Index));

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = User.Identity?.IsAuthenticated == true ? User.Identity?.Name : null,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            TotalAmount = lines.Sum(l => l.Product.Price * l.Quantity),
            Items = lines.Select(l => new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = Guid.Empty, // replaced after order Id set by EF tracking
                ProductId = l.Product.Id,
                Quantity = l.Quantity,
                UnitPrice = l.Product.Price
            }).ToList()
        };

        foreach (var item in order.Items)
            item.OrderId = order.Id;

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await _cart.ClearAsync();
        return RedirectToAction(nameof(Success), new { id = order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Success(Guid id)
    {
        var order = await _db.Orders.Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return RedirectToAction(nameof(Index));
        return View(order);
    }
}

public record AddToCartRequest(int ProductId, int Quantity);

