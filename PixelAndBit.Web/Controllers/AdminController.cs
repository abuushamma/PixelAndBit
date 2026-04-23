using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PixelAndBit.Domain.Enums;
using PixelAndBit.Domain.Entities;
using PixelAndBit.Infrastructure.Data;
using PixelAndBit.Web.Models;

namespace PixelAndBit.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly PixelBitDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public AdminController(PixelBitDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var totalProducts = await _db.Products.CountAsync();
        var pendingRepairs = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending);
        var totalRequests = await _db.Bookings.CountAsync();
        var totalUsers = await _db.Users.CountAsync();

        var servicesRevenueDouble = await _db.Bookings
            .Where(b => b.Status == BookingStatus.Completed)
            .SumAsync(b => (double?)b.EstimatedCost) ?? 0d;
        var servicesRevenue = (decimal)servicesRevenueDouble;

        var salesRevenueDouble = await _db.Orders
            .Where(o => o.Status == OrderStatus.Completed)
            .SumAsync(o => (double?)o.TotalAmount) ?? 0d;
        var salesRevenue = (decimal)salesRevenueDouble;

        var topDevices = await _db.Bookings
            .Where(b => b.DeviceModel != null && b.DeviceModel != "")
            .GroupBy(b => b.DeviceModel)
            .Select(g => new { DeviceModel = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.DeviceModel)
            .Take(8)
            .ToListAsync();

        var vm = new AdminDashboardVm
        {
            TotalProducts = totalProducts,
            PendingRepairs = pendingRepairs,
            TotalRequests = totalRequests,
            TotalUsers = totalUsers,
            ServicesRevenueJod = servicesRevenue,
            SalesRevenueJod = salesRevenue,
            TopDeviceModels = topDevices.Select(x => (x.DeviceModel, x.Count)).ToList()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var users = await _db.Users
            .OrderBy(u => u.Email)
            .ToListAsync();

        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var currentUserId = _userManager.GetUserId(User);
        if (string.Equals(currentUserId, id, StringComparison.Ordinal))
        {
            TempData["pb_toast_error"] = "You can't delete your own account.";
            return RedirectToAction(nameof(Users));
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            TempData["pb_toast_error"] = "Failed to delete user.";
            return RedirectToAction(nameof(Users));
        }

        TempData["pb_toast_ok"] = "User deleted.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> Orders()
    {
        var orders = await _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> OrderDetails(Guid id)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();
        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, OrderStatus status)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();

        order.Status = status;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(OrderDetails), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Products()
    {
        var products = await _db.Products
            .OrderBy(p => p.Name)
            .ToListAsync();
        return View(products);
    }

    [HttpGet]
    public IActionResult ProductCreate()
    {
        return View(new AdminProductEditVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProductCreate(AdminProductEditVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var p = new Product
        {
            Name = vm.Name.Trim(),
            Price = vm.Price,
            Description = vm.Description.Trim(),
            StockQuantity = vm.StockQuantity,
            ImageUrl = string.IsNullOrWhiteSpace(vm.ImageUrl) ? null : vm.ImageUrl.Trim()
        };

        _db.Products.Add(p);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Products));
    }

    [HttpGet]
    public async Task<IActionResult> ProductEdit(int id)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();

        var vm = new AdminProductEditVm
        {
            Id = p.Id,
            Name = p.Name,
            Price = p.Price,
            Description = p.Description,
            StockQuantity = p.StockQuantity,
            ImageUrl = p.ImageUrl
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProductEdit(int id, AdminProductEditVm vm)
    {
        if (id != vm.Id) return BadRequest();
        if (!ModelState.IsValid) return View(vm);

        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();

        p.Name = vm.Name.Trim();
        p.Price = vm.Price;
        p.Description = vm.Description.Trim();
        p.StockQuantity = vm.StockQuantity;
        p.ImageUrl = string.IsNullOrWhiteSpace(vm.ImageUrl) ? null : vm.ImageUrl.Trim();

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Products));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProductDelete(int id)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();

        _db.Products.Remove(p);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Products));
    }
}

