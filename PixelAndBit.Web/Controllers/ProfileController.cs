using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PixelAndBit.Infrastructure.Data;

namespace PixelAndBit.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly PixelBitDbContext _db;

    public ProfileController(PixelBitDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> MyOrders()
    {
        var userId = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userId)) return View(Array.Empty<PixelAndBit.Domain.Entities.Order>());

        var orders = await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> MyRepairs()
    {
        var userId = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userId)) return View(Array.Empty<PixelAndBit.Domain.Entities.Booking>());

        var repairs = await _db.Bookings
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return View(repairs);
    }
}

