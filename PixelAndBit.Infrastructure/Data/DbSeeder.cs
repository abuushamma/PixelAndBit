using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PixelAndBit.Domain.Entities;

namespace PixelAndBit.Infrastructure.Data;

public static class DbSeeder
{
    public const string AdminEmail = "admin@pixelbit.jo";
    private const int SeedPasswordMaxAttempts = 6;

    /// <summary>
    /// Password resolution (first non-empty wins): environment <c>ADMIN_SEED_PASSWORD</c>, then
    /// configuration <c>AdminSeed:Password</c> / <c>AdminSeed__Password</c> (Azure-style).
    /// In Development, if both are empty, a strong random password is generated and written to
    /// the <paramref name="logger"/> at <see cref="LogLevel.Warning"/> (console).
    /// </summary>
    public static async Task SeedAsync(
        PixelBitDbContext context,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ILogger logger,
        bool isDevelopment)
    {
        await EnsureRolesAsync(roleManager);
        await EnsureAdminAsync(userManager, configuration, logger, isDevelopment);

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

    private static async Task EnsureAdminAsync(
        UserManager<IdentityUser> userManager,
        IConfiguration configuration,
        ILogger logger,
        bool isDevelopment)
    {
        var admin = await userManager.FindByEmailAsync(AdminEmail);
        if (admin == null)
        {
            var adminPassword = ResolveAdminPassword(configuration, logger, isDevelopment);

            admin = new IdentityUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true
            };

            IdentityResult? created = null;
            for (var attempt = 0; attempt < SeedPasswordMaxAttempts; attempt++)
            {
                if (attempt > 0)
                    adminPassword = GenerateStrongSeedPassword();

                created = await userManager.CreateAsync(admin, adminPassword);
                if (created.Succeeded)
                {
                    if (attempt > 0 && isDevelopment)
                        logger.LogWarning(
                            "Admin user created after password regeneration (attempt {Attempt}). " +
                            "Set AdminSeed:Password or ADMIN_SEED_PASSWORD to avoid this.",
                            attempt + 1);
                    break;
                }
            }

            if (created is null || !created.Succeeded)
            {
                var msg = string.Join("; ", (created?.Errors ?? []).Select(e => e.Description));
                throw new InvalidOperationException($"Failed to seed admin user: {msg}");
            }
        }
        else
        {
            // New installs only; do not change existing admin password
            if (!admin.LockoutEnabled)
                await userManager.SetLockoutEnabledAsync(admin, true);
        }

        if (await userManager.IsInRoleAsync(admin, "Admin") == false)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }

    private static string ResolveAdminPassword(
        IConfiguration configuration,
        ILogger logger,
        bool isDevelopment)
    {
        var fromEnv = Environment.GetEnvironmentVariable("ADMIN_SEED_PASSWORD");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        var fromConfig = configuration["AdminSeed:Password"] ?? configuration["AdminSeed__Password"];
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return fromConfig.Trim();

        if (isDevelopment)
        {
            var password = GenerateStrongSeedPassword();
            logger.LogWarning(
                "==== ADMIN SEED (Development) ==== Email: {AdminEmail} One-time password: {AdminPassword} " +
                "Store this in User Secrets or set environment variable ADMIN_SEED_PASSWORD, then rotate after first login. " +
                "==================================",
                AdminEmail,
                password);
            return password;
        }

        throw new InvalidOperationException(
            "Admin seeding requires a password. Set environment variable ADMIN_SEED_PASSWORD " +
            "or configuration key AdminSeed:Password (e.g. dotnet user-secrets or Azure App Settings).");
    }

    /// <summary>Meets strong Identity password rules: length, classes, and ≥3 unique characters.</summary>
    private static string GenerateStrongSeedPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digit = "23456789";
        const string special = "!@#$%&*";
        for (var round = 0; round < 20; round++)
        {
            var all = upper + lower + digit + special;
            var len = 16;
            var sb = new StringBuilder(len);
            sb.Append(Pick(upper)).Append(Pick(lower)).Append(Pick(digit)).Append(Pick(special));
            for (var i = 4; i < len; i++)
                sb.Append(Pick(all));

            var s = new string(ShuffleString(sb).ToArray());
            if (s.Distinct().Count() < 3)
                continue;
            if (s.Length < 8)
                continue;
            return s;
        }

        // Fallback: extremely unlikely
        return "Aa1!Xy9" + Convert.ToHexString(SHA256.HashData(RandomNumberGenerator.GetBytes(4)))[..4];

        static char Pick(string set)
        {
            var b = new byte[1];
            RandomNumberGenerator.Fill(b);
            return set[b[0] % set.Length];
        }

        static IEnumerable<char> ShuffleString(StringBuilder input)
        {
            var list = input.ToString().ToCharArray();
            var b = new byte[4];
            for (var i = list.Length; i > 1; i--)
            {
                RandomNumberGenerator.Fill(b);
                var j = BitConverter.ToUInt32(b) % (uint)i;
                (list[i - 1], list[j]) = (list[j], list[i - 1]);
            }

            return list;
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
