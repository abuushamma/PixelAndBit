using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PixelAndBit.Domain.Entities;

namespace PixelAndBit.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        PixelBitDbContext context,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        await EnsureRolesAsync(roleManager);
        await EnsureAdminAsync(userManager, roleManager);

        if (!await context.Products.AnyAsync())
        {
            var products = CreateProducts();
            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { "Admin", "Customer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task EnsureAdminAsync(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        const string adminEmail = "admin@pixelbit.jo";
        const string adminPassword = "Admin@Pb2026!";

        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin == null)
        {
            admin = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var created = await userManager.CreateAsync(admin, adminPassword);
            if (!created.Succeeded)
            {
                var msg = string.Join("; ", created.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to seed admin user: {msg}");
            }
        }

        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }

    private static List<Product> CreateProducts() =>
    [
        new()
        {
            Name = "High-Performance Thermal Paste Application",
            Price = 10m,
            Description = "Professional thermal paste application for lower temps and better sustained performance.",
            StockQuantity = 50,
            ImageUrl = null
        },
        new()
        {
            Name = "Custom Mechanical Keyboard Modding",
            Price = 50m,
            Description = "Switch lubing, stabilizer tuning, foam modding, and custom feel/sound profiling.",
            StockQuantity = 20,
            ImageUrl = null
        },
        new()
        {
            Name = "Gaming Console Internal Cleaning (PS5/Xbox)",
            Price = 15m,
            Description = "Deep internal dust cleaning + airflow check to reduce fan noise and heat.",
            StockQuantity = 30,
            ImageUrl = null
        },
        new()
        {
            Name = "Retro Console HDMI Modding",
            Price = 80m,
            Description = "Clean HDMI output upgrade for classic consoles (compatibility depends on model).",
            StockQuantity = 10,
            ImageUrl = null
        },
        new()
        {
            Name = "Pixel&Bit Signature Gaming Build (Entry Level)",
            Price = 450m,
            Description = "Curated entry-level build tuned for 1080p gaming with clean cable management.",
            StockQuantity = 5,
            ImageUrl = null
        },
    ];
}
