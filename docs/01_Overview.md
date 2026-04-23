# 01 — Project Overview & Architecture

## 1. What Pixel & Bit is

Pixel & Bit is a small-business web application for a **tech-repair shop** in Amman, Jordan. It combines four things in one ASP.NET Core 8 web app:

1. **Booking** — a customer fills a form ("name, phone, device, issue") and receives a **unique ticket reference** (`PB-2026-XXXX`). They can come back any time and **track** the status.
2. **Store** — a public catalog of services/products. Visitors add to a **cart** (kept in server session, not DB) and confirm an **order**.
3. **Admin** — an authenticated admin with the `Admin` role manages products, views/edits orders, watches a live dashboard (totals + revenue), inspects users, and moves booking statuses forward.
4. **Accounts** — ASP.NET Core Identity with:
   - Email + password login
   - Public registration gated by a single `AllowPublicRegistration` flag
   - Custom **5-digit email verification** code flow (`EmailVerificationCodes` table)
   - Custom **multi-step forgot-password** flow (session-backed, reuses Identity's `GeneratePasswordResetTokenAsync`)
   - Role-based authorization (`Admin` role) on the admin surface

All UI text is **localized (en / ar)**. Arabic renders RTL with Bootstrap RTL + custom overrides. The visual theme is a dark "premium" look (glassmorphism, gradient accents, neon glow buttons) — one shared stylesheet: `wwwroot/css/pixelbit.css`.

---

## 2. Solution / folder tree

```
PixelAndBit/
├── PixelAndBit.sln
├── README_ADMIN.md                     # Operator cheat-sheet
├── scripts/
│   ├── start-dev.ps1                   # dotnet watch run — keep window open
│   └── start-dev.bat
├── tools/PixelAndBit.ImageTool/        # Helper (image processing; not in Web)
├── publish*/                           # Old dotnet publish outputs (deploy artifacts)
│
├── PixelAndBit.Domain/                 # Pure C# (no ASP.NET/EF deps)
│   ├── Entities/
│   │   ├── Product.cs
│   │   ├── Order.cs
│   │   ├── OrderItem.cs
│   │   ├── Booking.cs
│   │   ├── Appointment.cs
│   │   ├── RepairService.cs
│   │   └── EmailVerificationCode.cs
│   └── Enums/
│       ├── OrderStatus.cs
│       ├── BookingStatus.cs
│       ├── AppointmentStatus.cs
│       └── ProductCondition.cs
│
├── PixelAndBit.Application/            # Abstractions only
│   └── Interfaces/
│       ├── IProductService.cs
│       ├── ICartService.cs  (defines CartLine record)
│       ├── IBookingService.cs
│       ├── IAppointmentService.cs
│       └── IEmailSender.cs
│
├── PixelAndBit.Infrastructure/         # EF Core + implementations
│   ├── Data/
│   │   ├── AppDbContext.cs             # PixelBitDbContext : IdentityDbContext<IdentityUser>
│   │   ├── DbSeeder.cs                 # Roles, admin user, seed products
│   │   ├── ProductService.cs           # IProductService
│   │   ├── BookingService.cs           # IBookingService + ticket generator
│   │   ├── AppointmentService.cs       # IAppointmentService (partial)
│   │   ├── CartService.cs              # ICartService (session-backed)
│   │   └── Configurations/             # Fluent EF per-entity configs
│   │       ├── ProductConfiguration.cs
│   │       ├── OrderConfiguration.cs
│   │       ├── OrderItemConfiguration.cs
│   │       ├── BookingConfiguration.cs
│   │       ├── AppointmentConfiguration.cs
│   │       ├── RepairServiceConfiguration.cs
│   │       └── EmailVerificationCodeConfiguration.cs
│   ├── Email/
│   │   ├── SmtpEmailSender.cs          # MailKit — real sender
│   │   └── NullEmailSender.cs          # Logs only; used when SMTP not configured
│   └── Migrations/
│       ├── 20260418124033_InitialSqlite.cs
│       └── PixelBitDbContextModelSnapshot.cs
│
└── PixelAndBit.Web/                    # The ASP.NET Core host
    ├── Program.cs                      # Composition root
    ├── PixelAndBit.Web.csproj          # TFM: net8.0
    ├── appsettings.json / *.Development.json / *.Production.json
    ├── pixelbit.db                     # SQLite file (dev)
    │
    ├── Controllers/                    # 8 controllers (see 02_Backend.md)
    │   ├── HomeController.cs
    │   ├── StoreController.cs
    │   ├── BookingController.cs
    │   ├── CartController.cs
    │   ├── ProfileController.cs
    │   ├── AdminController.cs
    │   ├── AdminUsersApiController.cs  # JSON API under /api/admin
    │   └── LanguageController.cs
    │
    ├── Models/                         # ViewModels + DTOs
    │   ├── CreateBookingVm.cs
    │   ├── TrackBookingVm.cs
    │   ├── AdminDashboardVm.cs
    │   ├── AdminProductEditVm.cs
    │   └── ErrorViewModel.cs
    │
    ├── Resources/                      # Localization
    │   ├── SharedResource.cs           # marker class
    │   ├── SharedResource.resx         # English strings
    │   └── SharedResource.ar.resx      # Arabic strings
    │
    ├── Areas/Identity/Pages/Account/   # Razor Pages for auth
    │   ├── Login.cshtml(.cs)
    │   ├── Register.cshtml(.cs)
    │   ├── Logout.cshtml(.cs)
    │   ├── AccessDenied.cshtml(.cs)
    │   ├── RegisterConfirmation.cshtml(.cs)
    │   ├── VerifyEmail.cshtml(.cs)     # 5-digit email verify
    │   ├── ForgotPassword.cshtml(.cs)  # Reset — step 1
    │   ├── VerifyResetCode.cshtml(.cs) # Reset — step 2
    │   ├── ResetPassword.cshtml(.cs)   # Reset — step 3
    │   └── PasswordResetFlow.cs        # session keys + helpers + reset email HTML
    │
    ├── Views/
    │   ├── _ViewImports.cshtml, _ViewStart.cshtml
    │   ├── Shared/
    │   │   ├── _Layout.cshtml
    │   │   ├── _Navbar.cshtml
    │   │   ├── Error.cshtml
    │   │   └── _ValidationScriptsPartial.cshtml
    │   ├── Home/       Index, Privacy, Contact
    │   ├── Store/      Index
    │   ├── Booking/    Index, Track, Success, Admin
    │   ├── Cart/       Index, Success
    │   ├── Profile/    Index, MyOrders, MyRepairs
    │   └── Admin/      Index, Users, Products, ProductCreate, ProductEdit, Orders, OrderDetails
    │
    └── wwwroot/
        ├── favicon.ico, pixelbit-nav-icon.png
        ├── css/   site.css, pixelbit.css (large, ~2800 lines), tailwind.bundle.css
        ├── js/    site.js (fade-in, parallax, navbar scroll, device chips, cart AJAX)
        └── lib/   bootstrap, jquery, jquery-validation(-unobtrusive)
```

---

## 3. Architecture at a glance

```
                              Browser (Bootstrap + pixelbit.css + site.js)
                                         │
                                         ▼
┌───────────────────────────────────── Web (ASP.NET Core 8) ─────────────────────────────────────┐
│  Program.cs — composition root, localization, Identity, SQLite, sessions, SMTP, routing        │
│                                                                                                │
│  Controllers (MVC)                    Razor Pages (Areas/Identity)                             │
│    Home, Store, Cart, Booking,          Login, Register, VerifyEmail,                           │
│    Profile, Admin, AdminUsersApi,       ForgotPassword, VerifyResetCode, ResetPassword,         │
│    Language                             Logout, AccessDenied                                    │
│                                                                                                │
│  Depend on interfaces from Application ──▶ IProductService, IBookingService,                    │
│                                            ICartService, IAppointmentService, IEmailSender      │
└────────────────────────────────┬───────────────────────────────────────────────────────────────┘
                                 │
                                 ▼
┌───────────────────── Infrastructure ─────────────────────┐   ┌──────── Application (abstractions) ────────┐
│  PixelBitDbContext : IdentityDbContext<IdentityUser>     │   │  IProductService   ICartService            │
│  Services: Product/Booking/Appointment/Cart (EF/session) │   │  IBookingService   IAppointmentService     │
│  Email: SmtpEmailSender / NullEmailSender                │   │  IEmailSender      (+ CartLine record)     │
│  Fluent configs + migrations                             │   └────────────────────────────────────────────┘
└───────────────────┬──────────────────────────────────────┘
                    │
                    ▼
             ┌─────────── Domain ───────────┐
             │ Product, Order, OrderItem,   │
             │ Booking, Appointment,        │
             │ RepairService,               │
             │ EmailVerificationCode        │
             │ + enums                      │
             └──────────────────────────────┘

                       ┌────── External I/O ───────┐
                       │ SQLite file: pixelbit.db   │
                       │ SMTP (Gmail by default)   │
                       └───────────────────────────┘
```

**Key dependency rule**: `Web` → `Application` + `Infrastructure` → `Domain`.
- `Domain` has no dependencies.
- `Application` only depends on `Domain`.
- `Infrastructure` implements the interfaces from `Application` using EF Core + `Domain` entities.
- `Web` is the only layer that references everything and wires DI in `Program.cs`.

---

## 4. Typical request flow

### 4.1 "Book a repair" (happy path)
1. Visitor browses `/Booking` → returns `Views/Booking/Index.cshtml` bound to `CreateBookingVm`.
2. Form posts to `BookingController.Confirm(vm)` with `@Html.AntiForgeryToken()`.
3. Controller calls `IBookingService.CreateBookingAsync(...)` → `BookingService` validates (Jordanian phone `^07\d{8}$`, description ≥ 20 chars, etc.), generates `PB-YYYY-XXXX` (base-36, retries on collision), persists `Booking`, returns `(ok, ticket, err)`.
4. Controller redirects to `Booking/Success?ticket=PB-2026-1A2B`, and the success view shows the ticket for the customer to save.

### 4.2 "Track my repair"
1. Visitor hits `/Booking/Track`, submits a ticket reference.
2. `BookingController.Track(vm)` calls `IBookingService.GetByTicketAsync(...)` → uppercased lookup against `Bookings.TicketReference` (unique index).
3. The view renders the current `BookingStatus` badge; if not found, it adds a model error.

### 4.3 "Shop + checkout" (anonymous)
1. `/Store` → `StoreController.Index` lists all products and supports `?q=` simple search (`Name`/`Description`, case-insensitive).
2. Each card has a POST button → `CartController.Add([FromBody] AddToCartRequest)` (AJAX). `CartService` stores `Dictionary<int,int>` in **session** under `pb_cart_v1`; returns `{count, total}`.
3. `/Cart` → `CartController.Index` re-hydrates lines from the DB by product IDs.
4. `Confirm` creates a real `Order` + `OrderItem[]` in DB, clears the session cart, redirects to `/Cart/Success/{id}`.

### 4.4 Sign in / Register / Verify
1. `/Identity/Account/Register` (enabled by `AllowPublicRegistration=true`) creates the `IdentityUser` with `EmailConfirmed = false`.
2. A random 5-digit code is generated, salted-hashed (`SHA256(userId|EMAIL|code)`), and persisted in `EmailVerificationCodes` with a 10-minute TTL + 6-attempt cap.
3. User is redirected to `/Identity/Account/VerifyEmail?email=...`. JS auto-submits when 5 digits are entered.
4. Correct code → `user.EmailConfirmed = true` + `SignInAsync`.
5. `/Identity/Account/Login` is the shared form for users and admins; after login, if the user has the `Admin` role, the navbar shows the Admin link and `[Authorize(Roles="Admin")]` permits `/Admin/*`.

### 4.5 Forgot password (multi-step, session-backed)
See `Areas/Identity/Pages/Account/PasswordResetFlow.cs`:
- **Step 1 /ForgotPassword** — always stores the email in session (even when not found), generates code + Identity `GeneratePasswordResetTokenAsync`, sends an email, redirects to Step 2.
- **Step 2 /VerifyResetCode** — gated on `HasPendingEmail`; `SHA256("reset|userId|EMAIL|code")` + `FixedTimeEquals`; attempts counter incremented before comparison; after success sets `verified=1`, removes the code hash.
- **Step 3 /ResetPassword** — gated on `IsCodeVerified`; calls `UserManager.ResetPasswordAsync(user, token, newPw)` + `UpdateSecurityStampAsync`; wipes session state; redirects to Login with toast.

---

## 5. `Program.cs` — composition root walkthrough

File: `PixelAndBit.Web/Program.cs`

### 5.1 Kestrel binding
```csharp
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    if (builder.Environment.IsDevelopment())
        builder.WebHost.UseUrls("http://0.0.0.0:5001"); // LAN + localhost for mobile testing
    else
        builder.WebHost.UseUrls("http://127.0.0.1:5001"); // loopback only; reverse-proxy terminates TLS
}
```
You can override with `ASPNETCORE_URLS`. Production is loopback‑only on purpose: deploy behind Nginx.

### 5.2 SQLite path + config override
```csharp
var sqlitePath = Path.Combine(builder.Environment.ContentRootPath, "pixelbit.db");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:PixelBitConnection"] = $"Data Source={sqlitePath}"
});
```
The DB file always sits next to the executable regardless of where the process was launched from.

### 5.3 Logging
Clears providers, adds `SimpleConsole` (with timestamp) + Debug, default level `Information`.

### 5.4 MVC, Razor Pages, localization
```csharp
builder.Services.AddLocalization();
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(o =>
        o.DataAnnotationLocalizerProvider = (_, f) => f.Create(typeof(SharedResource)));
builder.Services.AddRazorPages();
```
`SharedResource.cs` is an empty marker class; the localizer resolves strings from `Resources/SharedResource.resx` (and `.ar.resx`).

### 5.5 HTTP infrastructure
- `AddHttpContextAccessor()` — so `CartService` and `PasswordResetFlow` can read session.
- `AddDistributedMemoryCache()` — backing store for the session.
- `AddResponseCompression()` — Brotli + Gzip for HTTPS.
- `AddSession()` — cookie name `.PixelAndBit.Session`, `HttpOnly=true`, `IsEssential=true`, `IdleTimeout=6h`.

### 5.6 Application services
```csharp
builder.Services.AddScoped<IProductService,     ProductService>();
builder.Services.AddScoped<IBookingService,     BookingService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<ICartService,        CartService>();
```

### 5.7 Email sender — conditional on SMTP config
```csharp
builder.Services.Configure<SmtpEmailOptions>(builder.Configuration.GetSection("Smtp"));
var smtpConfigured = !string.IsNullOrWhiteSpace(smtpHost) &&
                     !smtpHost.Contains("YOUR_", ...) &&
                     !string.IsNullOrWhiteSpace(smtpFrom) && ...;
if (smtpConfigured) services.AddScoped<IEmailSender, SmtpEmailSender>();
else                services.AddSingleton<IEmailSender, NullEmailSender>();
```
This is why the site starts even without SMTP configured: email calls become no-ops.

### 5.8 EF Core + Identity
```csharp
builder.Services.AddDbContext<PixelBitDbContext>(opts =>
    opts.UseSqlite(sqliteConnectionString,
        sql => sql.MigrationsAssembly("PixelAndBit.Infrastructure")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(o =>
{
    o.Password.RequireDigit = false;
    o.Password.RequiredLength = 6;
    o.Password.RequireNonAlphanumeric = false;
    o.Password.RequireUppercase = false;
    o.SignIn.RequireConfirmedAccount = true; // email verification enforced
})
.AddEntityFrameworkStores<PixelBitDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();
```

### 5.9 Cookie/auth UX
```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin        = ctx => /* API paths → 401 instead of redirect */,
        OnRedirectToAccessDenied = ctx => /* API paths → 403 instead of redirect */
    };
});
```
Browsers see HTML redirects; `/api/*` callers get proper HTTP status codes — important for `AdminUsersApiController`.

### 5.10 Lazy DB initialization
```csharp
Task? dbInitTask = null;
Task EnsureDatabaseInitializedAsync() { /* migrate + (Development) seed, run-once */ }
app.Use(async (ctx, next) => { await EnsureDatabaseInitializedAsync(); await next(); });
```
Hot-reload-friendly: if `dotnet watch` doesn't restart the process, the DB still migrates on the next request. `/__health/db` returns `{ ok, products, database, file }`.

### 5.11 Pipeline order
```
UseRequestLocalization → (DbInit middleware) →
(Dev) UseDeveloperExceptionPage | (Prod) UseExceptionHandler("/Home/Error") →
UseStaticFiles → UseRouting → UseResponseCompression → UseSession →
UseAuthentication → UseAuthorization →
MapGet("/__health/db") → MapControllerRoute(default "{controller=Home}/{action=Index}/{id?}") →
MapControllers → MapRazorPages
```

### 5.12 Graceful port-in-use handling
```csharp
catch (Exception ex) when (IsAddressAlreadyInUse(ex)) { log "port busy"; throw; }
```

---

## 6. Project-level design notes

- **Clean boundary**: controllers never `new DbContext()`. They always go through the service interfaces (`IProductService`, `IBookingService`, `ICartService`, `IAppointmentService`). The notable exceptions are `ProfileController` and `AdminController`, which query the `PixelBitDbContext` directly because those are read-heavy admin/reporting queries.
- **Session over DB for cart**: the cart is not a DB entity. It's a `Dictionary<int, int>` serialized as JSON in session (`pb_cart_v1`). This keeps anonymous checkout frictionless.
- **Ticket generation**: `Booking.TicketReference` is unique; `BookingService.GenerateTicketReferenceAsync` uses secure RNG + base-36 with retry loop to avoid collisions, fallback to a GUID-based suffix.
- **Verification codes are hashed**: raw codes never hit the DB. `HashCode(userId, email, code) = SHA256Hex(userId|EMAIL_UPPER|code)`; comparison uses `CryptographicOperations.FixedTimeEquals` for constant time. Reset codes use the same pattern with a `"reset|"` purpose marker to isolate hash spaces.
- **No custom password tokens**: the actual password mutation uses Identity's own `GeneratePasswordResetTokenAsync` + `ResetPasswordAsync`. Our 5-digit code is only a user-facing gate that unlocks that built-in token stored in session.
- **Localization is centralized**: one resource (`SharedResource.resx`). Controllers and views use `@inject IStringLocalizer<SharedResource> T`. Culture is cookie-based via `LanguageController.Set`.
- **Bidi support**: `_Layout.cshtml` picks the RTL Bootstrap bundle when culture is `ar`; `pixelbit.css` has `.pb-rtl` overrides.

Continue to [`02_Backend.md`](./02_Backend.md) for the per-class file-by-file tour.
