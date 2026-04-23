using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Localization;
using PixelAndBit.Application.Interfaces;
using PixelAndBit.Infrastructure.Email;
using PixelAndBit.Infrastructure.Data;
using System.Globalization;
using System.Net.Sockets;

var builder = WebApplication.CreateBuilder(args);

// Respect ASPNETCORE_URLS (launchSettings / systemd). If unset: Production = loopback only; Development = all interfaces (easier local access).
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    if (builder.Environment.IsDevelopment())
        builder.WebHost.UseUrls("http://0.0.0.0:5001");
    else
        builder.WebHost.UseUrls("http://127.0.0.1:5001");
}

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddLocalization();

builder.Services
    .AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(PixelAndBit.Web.SharedResource));
    });
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".PixelAndBit.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(6);
});
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<ICartService, CartService>();

builder.Services.Configure<SmtpEmailOptions>(builder.Configuration.GetSection("Smtp"));
var smtpHost = builder.Configuration["Smtp:Host"];
var smtpFrom = builder.Configuration["Smtp:FromEmail"];
var smtpConfigured =
    !string.IsNullOrWhiteSpace(smtpHost) &&
    !smtpHost.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) &&
    !string.IsNullOrWhiteSpace(smtpFrom) &&
    !smtpFrom.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);

if (smtpConfigured)
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, NullEmailSender>();

var sqlServerConnectionString =
    builder.Configuration.GetConnectionString("PixelBitConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'PixelBitConnection' not found in configuration.");

builder.Services.AddDbContext<PixelBitDbContext>(options =>
    options.UseSqlServer(
        sqlServerConnectionString,
        sql => sql.MigrationsAssembly("PixelAndBit.Infrastructure")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.SignIn.RequireConfirmedAccount = true;
})
.AddEntityFrameworkStores<PixelBitDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
startupLogger.LogInformation(
    "Application starting — Environment={Env}, ContentRoot={Root}, Database=SQL Server",
    app.Environment.EnvironmentName,
    app.Environment.ContentRootPath);

// Ensure the DB is migrated + seeded once. We do this lazily on first request too,
// because `dotnet watch` hot reload may not restart the process (so startup code won't rerun).
Task? dbInitTask = null;
var dbInitLock = new object();

Task EnsureDatabaseInitializedAsync()
{
    lock (dbInitLock)
    {
        dbInitTask ??= Task.Run(async () =>
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<PixelBitDbContext>();

                startupLogger.LogInformation("Applying database migrations (SQL Server)...");
                await db.Database.MigrateAsync();
                startupLogger.LogInformation("Database migrations applied successfully.");

                if (app.Environment.IsDevelopment())
                {
                    startupLogger.LogInformation("Seeding initial data (Development)...");
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                    await DbSeeder.SeedAsync(db, userManager, roleManager);
                    startupLogger.LogInformation("Database seeding complete.");
                }
            }
            catch (Exception ex)
            {
                startupLogger.LogWarning(ex, "Database migration/seed failed; continuing without blocking startup.");
            }
        });
    }

    return dbInitTask;
}

var supportedCultures = new[] { "en", "ar" }
    .Select(c => new CultureInfo(c))
    .ToList();

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
    RequestCultureProviders = new IRequestCultureProvider[]
    {
        new CookieRequestCultureProvider()
    }
});

app.Use(async (ctx, next) =>
{
    await EnsureDatabaseInitializedAsync();
    await next();
});

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler("/Home/Error");

app.UseStaticFiles();
app.UseRouting();

app.UseResponseCompression();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/__health/db", async (PixelBitDbContext db) =>
{
    await EnsureDatabaseInitializedAsync();
    var count = await db.Products.CountAsync();
    return Results.Ok(new
    {
        ok = true,
        products = count,
        database = "sqlserver"
    });
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();
app.MapRazorPages();

startupLogger.LogInformation(
    "Kestrel listening — open http://localhost:5001 (Development binds 0.0.0.0:5001 unless ASPNETCORE_URLS is set).");
try
{
    await app.RunAsync();
}
catch (Exception ex) when (IsAddressAlreadyInUse(ex))
{
    startupLogger.LogCritical(
        ex,
        "Port binding failed — http://127.0.0.1:5000 is already in use. Stop the other process or ensure a single systemd/Kestrel instance.");
    throw;
}

static bool IsAddressAlreadyInUse(Exception ex)
{
    for (var e = ex; e != null; e = e.InnerException)
    {
        if (e is SocketException se && se.SocketErrorCode == SocketError.AddressAlreadyInUse)
            return true;
    }

    return ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase);
}
