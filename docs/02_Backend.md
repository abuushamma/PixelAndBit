# 02 — Backend: Controllers, Services, Interfaces, Entities, Identity Pages

This file walks every backend file (C# side). Sections:

1. **Domain** — entities + enums
2. **Application** — interface contracts
3. **Infrastructure** — services + email + seeder + EF configurations
4. **Web: Controllers** — every controller and every action
5. **Web: ViewModels**
6. **Web: Razor Pages (Identity)** — login / register / verify / reset

---

## 1. Domain

Pure C# classes under `PixelAndBit.Domain/`. No ASP.NET/EF references. Safe to share.

### 1.1 Entities

#### `Entities/Product.cs`
```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string Description { get; set; } = "";
    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }
}
```
A product/service in the store catalog. `Price` is stored as `decimal(10,2)` (see `ProductConfiguration`). `ImageUrl` is optional.

#### `Entities/Order.cs`
```csharp
public class Order
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }                  // nullable → guest checkout allowed
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
```

#### `Entities/OrderItem.cs`
```csharp
public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }               // price at time of order (snapshot)
}
```
Uses a **price snapshot** so historical orders stay correct even if the product price changes later.

#### `Entities/Booking.cs`
```csharp
public class Booking
{
    public Guid Id { get; set; }
    public string TicketReference { get; set; } = "";    // e.g. "PB-2026-1A2B" (unique)
    public string? UserId { get; set; }                  // nullable → guests may book
    public string CustomerName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";        // Jordan mobile format validated
    public string DeviceModel { get; set; } = "";
    public string IssueDescription { get; set; } = "";
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal EstimatedCost { get; set; }
}
```

#### `Entities/Appointment.cs`
Schedulable appointment (richer than `Booking` — has date/time slots + linked `RepairService`). Used for future slot-based scheduling; the current UI is booking-centric, not appointment-centric, so `AppointmentService` exposes placeholders for `GetAvailableDatesAsync` / `GetAvailableSlotsAsync`.

#### `Entities/RepairService.cs`
Catalog of repair service types with `BasePrice` and `DurationMinutes` (used for slot generation). Navigation: `ICollection<Appointment> Appointments`.

#### `Entities/EmailVerificationCode.cs`
```csharp
public class EmailVerificationCode
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string CodeHash { get; set; } = "";           // SHA-256 hex
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 6;
    public DateTime? ConsumedAtUtc { get; set; }
}
```
One row per issued code. Used by `VerifyEmailModel`. Forgot-password codes do **not** use this table — they live in server session.

### 1.2 Enums

| Enum | Values |
|---|---|
| `BookingStatus` | `Pending = 0, Received = 1, InProgress = 2, ReadyForPickup = 3, Completed = 4` |
| `OrderStatus` | `Pending = 0, Completed = 1, Cancelled = 2` |
| `AppointmentStatus` | `Pending, Confirmed, InProgress, Completed, Cancelled` |
| `ProductCondition` | `New, Refurbished, Used` (defined but not yet used on `Product`) |

All enums with DB persistence are stored as **strings** via `HasConversion<string>()` in Fluent configs, so enum values are readable in SQL and safe against numeric drift.

---

## 2. Application — abstractions

### `Interfaces/IProductService.cs`
```csharp
Task<IEnumerable<Product>> GetAllProductsAsync();
Task<Product?> GetProductByIdAsync(int id);
```

### `Interfaces/ICartService.cs`
```csharp
Task AddAsync(int productId, int quantity = 1);
Task RemoveAsync(int productId, int quantity = 1);
Task ClearAsync();
Task<IReadOnlyList<CartLine>> GetLinesAsync();
Task<int>      GetItemCountAsync();
Task<decimal>  GetTotalAsync();

public record CartLine(Product Product, int Quantity);
```
Abstracts the "cart" away from the HTTP session implementation. Any DI target can be swapped.

### `Interfaces/IBookingService.cs`
```csharp
Task<(bool Success, string? TicketReference, string? ErrorMessage)> CreateBookingAsync(
    string? userId, string customerName, string phoneNumber,
    string deviceModel, string issueDescription, decimal estimatedCost);

Task<IReadOnlyList<Booking>> GetAllAsync();
Task<Booking?>               GetByTicketAsync(string ticketReference);
Task<bool>                   UpdateStatusAsync(Guid bookingId, BookingStatus status);
```

### `Interfaces/IAppointmentService.cs`
```csharp
Task<IEnumerable<RepairService>> GetAllRepairServicesAsync();
Task<IEnumerable<DateTime>>      GetAvailableDatesAsync(int serviceId, int month, int year);
Task<IEnumerable<TimeSpan>>      GetAvailableSlotsAsync(int serviceId, DateTime date);
Task<bool>                       CreateAppointmentAsync(Appointment appointment);
```
Two methods are stubs (return empty) pending the future scheduling UI.

### `Interfaces/IEmailSender.cs`
```csharp
Task SendAsync(string toEmail, string subject, string htmlBody);
```
Single method. Two implementations in `Infrastructure`.

---

## 3. Infrastructure

### 3.1 `Data/AppDbContext.cs` (class: `PixelBitDbContext`)
```csharp
public class PixelBitDbContext : IdentityDbContext<IdentityUser>
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<RepairService> RepairServices => Set<RepairService>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);                                     // Identity tables
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PixelBitDbContext).Assembly);
    }
}
```
- Inherits from `IdentityDbContext<IdentityUser>` so the `AspNet*` tables are created automatically.
- `ApplyConfigurationsFromAssembly` auto-registers every `IEntityTypeConfiguration<T>` in the Infrastructure assembly.

### 3.2 `Data/Configurations/*`

| File | Maps | Notes |
|---|---|---|
| `ProductConfiguration.cs` | `Product` → `Products` | `Name` required ≤ 200, `Description` required ≤ 2000, `Price decimal(10,2)`, `ImageUrl` ≤ 500 |
| `BookingConfiguration.cs` | `Booking` → `Bookings` | `TicketReference` unique (`HasIndex(...).IsUnique()`), status stored as string, `EstimatedCost decimal(10,2)` |
| `OrderConfiguration.cs` | `Order` → `Orders` | `Status` as string, `TotalAmount decimal(10,2)`, cascade delete of `Items` |
| `OrderItemConfiguration.cs` | `OrderItem` → `OrderItems` | `UnitPrice decimal(10,2)`, FK to `Product` with `OnDelete(Restrict)` |
| `AppointmentConfiguration.cs` | `Appointment` → `Appointments` | `ConfirmationCode` unique, FK to `RepairService` with `OnDelete(Restrict)` |
| `RepairServiceConfiguration.cs` | `RepairService` → `RepairServices` | `BasePrice decimal(18,2)` |
| `EmailVerificationCodeConfiguration.cs` | `EmailVerificationCode` → `EmailVerificationCodes` | Index on `(UserId, Email, ConsumedAtUtc, ExpiresAtUtc)` for the verify-latest query |

### 3.3 `Data/DbSeeder.cs`

Static `SeedAsync(ctx, userManager, roleManager)` called from `Program.cs` only when `app.Environment.IsDevelopment()`.

Steps:
1. **Roles** — creates `"Admin"` and `"Customer"` if missing.
2. **Admin** — ensures `admin@pixelbit.jo` with password `Admin@Pb2026!`; marks `EmailConfirmed = true`; adds to `Admin` role.
3. **Seed products** — if the `Products` table is empty, inserts five curated rows (thermal paste, keyboard modding, console cleaning, HDMI mod, entry-level build).

> **Security note**: seeding is dev-only. In production you should change the admin password immediately and remove or edit the default.

### 3.4 `Data/ProductService.cs`

```csharp
public async Task<IEnumerable<Product>> GetAllProductsAsync()
    => await _context.Products.ToListAsync();

public async Task<Product?> GetProductByIdAsync(int id)
    => await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
```
No filtering or paging; the store is small. Search is done in `StoreController` in-memory after the `ToList`.

### 3.5 `Data/BookingService.cs`

**`CreateBookingAsync(...)`** performs:
- Input trimming + validation:
  - `customerName` length ≥ 2
  - phone matches `^07\d{8}$` (Jordanian mobile)
  - `deviceModel` length ≥ 2
  - `issueDescription` length ≥ 20
  - `estimatedCost ≥ 0`
- Ticket generation: `GenerateTicketReferenceAsync(year)`:
  - Up to **8 retries**, each with a 4-char base-36 suffix from `RandomNumberGenerator.Fill(bytes)` → `"PB-{year}-{SUFFIX}"`, collision-checked against DB.
  - Final fallback: `PB-{year}-{GuidNpart}` truncated to 21 chars.
- Persists the `Booking` and returns `(true, ticket, null)`.

**`GetByTicketAsync(ticket)`** — trims, uppercases, and does `FirstOrDefaultAsync` against the unique `TicketReference` index.

**`UpdateStatusAsync(id, status)`** — fetches by `Id`, mutates `Status`, saves.

**`GetAllAsync()`** — returns all bookings `OrderByDescending(CreatedAt)`.

### 3.6 `Data/AppointmentService.cs`

- `GetAllRepairServicesAsync()` — just `ToListAsync()` on `RepairServices`.
- `CreateAppointmentAsync(appointment)` — sets `ConfirmationCode = "PB-" + Guid.NewGuid()[0..5].ToUpper()`, saves; returns `true` if `SaveChangesAsync() > 0`.
- `GetAvailableDatesAsync` / `GetAvailableSlotsAsync` — intentionally return empty; placeholder for future slot calendar.

### 3.7 `Data/CartService.cs` (session-backed)

```csharp
private const string SessionKey = "pb_cart_v1";
// Stored value: JSON Dictionary<int productId, int quantity>
```
- Pulls `HttpContext` via `IHttpContextAccessor`.
- `GetCart()` — deserializes JSON; returns empty dict on error.
- `AddAsync / RemoveAsync / ClearAsync` — mutate and save JSON back.
- `GetLinesAsync()` — round-trips product IDs to `_db.Products`, then joins in memory:
```csharp
var ids = cart.Keys.ToArray();
var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToListAsync();
// → List<CartLine(Product, Quantity)>
```
- `GetItemCountAsync / GetTotalAsync` — derived from `GetLinesAsync`.

### 3.8 `Email/SmtpEmailSender.cs`

- Options class: `SmtpEmailOptions { Host, Port=587, EnableSsl=true, Username, Password, FromEmail, FromName="Pixel & Bit" }`.
- `SendAsync`:
  - Builds `MimeMessage` with `TextPart(Html)`.
  - Connects with `MailKit`, `timeout 30s`, uses `ConnectAsync(host, port, useSsl:true)`, `AuthenticateAsync`, `SendAsync`, disconnects.
  - Logs `=== SMTP DEBUG START / END / ERROR ===` blocks to stdout.
  - Rethrows on failure — callers (`VerifyEmailModel`, `ForgotPasswordModel`, etc.) wrap with `try/catch` and log without leaking details.

### 3.9 `Email/NullEmailSender.cs`

```csharp
public Task SendAsync(string toEmail, string subject, string htmlBody)
{
    _logger.LogDebug("Email not sent (SMTP not configured). To={To} Subject={Subject}", toEmail, subject);
    return Task.CompletedTask;
}
```
Registered when SMTP config is missing — keeps the app usable for local dev without real credentials.

### 3.10 Migrations

- `Migrations/20260418124033_InitialSqlite.cs` — initial migration creating:
  - Identity tables (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetRoleClaims`, `AspNetUserTokens`)
  - Business tables (`Bookings`, `Products`, `Orders`, `OrderItems`, `Appointments`, `RepairServices`, `EmailVerificationCodes`)
  - All indexes (including unique `TicketReference` and the `EmailVerificationCodes` composite index)
- `PixelBitDbContextModelSnapshot.cs` — EF state file.

Migration is applied automatically on first request via `EnsureDatabaseInitializedAsync()` in `Program.cs`.

---

## 4. Web — Controllers

All controllers live in `PixelAndBit.Web/Controllers`. Default route: `"{controller=Home}/{action=Index}/{id?}"`.

### 4.1 `HomeController.cs`

| Action | Verb | What it does |
|---|---|---|
| `Index()` | GET | Renders `Views/Home/Index.cshtml` (marketing homepage) |
| `Privacy()` | GET | Static privacy page |
| `Contact()` | GET | Static contact page |
| `Error()` | GET | `[ResponseCache(NoStore=true)]` — renders `Error.cshtml` with `ErrorViewModel { RequestId }` |

No services injected; just `ILogger<HomeController>` for diagnostics.

### 4.2 `StoreController.cs`

- Ctor: `IProductService`.
- `Index()` — loads all products, if `?q=...` filters in-memory on upper-cased `Name`/`Description`, sets `ViewBag.Q`, returns the list.

### 4.3 `BookingController.cs`

- Ctor: `IBookingService`.
- Actions:

| Action | Verb | Auth | Flow |
|---|---|---|---|
| `Index()` | GET | Public | Empty `CreateBookingVm` |
| `Confirm(vm)` | POST `[ValidateAntiForgeryToken]` | Public | Validates VM; concatenates `deviceType: deviceModel`; delegates to `IBookingService`; on success redirects to `Success?ticket=...`; on failure re-renders `Index.cshtml` with errors |
| `Success(ticket)` | GET | Public | `ViewBag.Ticket`; shows the ticket UI |
| `Admin()` | GET | `[Authorize(Roles="Admin")]` | `GetAllAsync`; if `?q=` filters by ticket/name/phone (in-memory) |
| `UpdateStatus([FromBody] req)` | POST `[ValidateAntiForgeryToken]` | Admin | JSON body `{ BookingId: Guid, Status: BookingStatus }`; returns `{ status: "InProgress" }` |
| `Track()` | GET | Public | Empty `TrackBookingVm` |
| `Track(vm)` | POST `[ValidateAntiForgeryToken]` | Public | Calls `GetByTicketAsync`; sets `vm.Result`; if null, adds `ModelState` error |

Authenticated bookings tag `UserId = User.Identity?.Name` (that's the email because `UserName = Email` in Identity).

### 4.4 `CartController.cs`

- Ctor: `ICartService` + `PixelBitDbContext` (direct DB access for `Orders`).
- Actions:

| Action | Verb | Notes |
|---|---|---|
| `Index()` | GET | Renders current cart lines + total |
| `Add([FromBody] AddToCartRequest)` | POST `[ValidateAntiForgeryToken]` | JSON `{ productId, quantity }`; product existence checked; returns `{count,total}` for the AJAX badge |
| `Clear()` | POST | Clears session cart, redirects to `Index` |
| `Confirm()` | POST | Creates `Order` + `OrderItem[]` (FK patched after assignment), clears cart, redirects to `Success/{id}` |
| `Success(Guid id)` | GET | Loads order (`Include Items → Product`) for display |

`record AddToCartRequest(int ProductId, int Quantity);`

### 4.5 `ProfileController.cs` — `[Authorize]` class-level

| Action | Returns |
|---|---|
| `Index()` | profile landing page |
| `MyOrders()` | Orders where `UserId == User.Identity?.Name`, ordered DESC |
| `MyRepairs()` | Bookings where `UserId == User.Identity?.Name`, ordered DESC |

### 4.6 `AdminController.cs` — `[Authorize(Roles="Admin")]`

Ctor: `PixelBitDbContext`, `UserManager<IdentityUser>`.

| Action | Verb | What it does |
|---|---|---|
| `Index()` | GET | Builds `AdminDashboardVm`: counts, revenue summed over completed bookings/orders (SQLite `SumAsync<double?>` cast to decimal), top 8 device models grouped by count |
| `Users()` | GET | `_db.Users.OrderBy(Email).ToListAsync()` |
| `DeleteUser(id)` | POST `[ValidateAntiForgeryToken]` | Refuses to delete current admin; `UserManager.DeleteAsync`; toast on error |
| `Orders()` | GET | All orders with `Items → Product`, DESC by date |
| `OrderDetails(Guid id)` | GET | Single order with items |
| `UpdateOrderStatus(id, status)` | POST `[ValidateAntiForgeryToken]` | Mutates `Order.Status` |
| `Products()` | GET | All products ordered by name |
| `ProductCreate()` | GET | Empty `AdminProductEditVm` |
| `ProductCreate(vm)` | POST `[ValidateAntiForgeryToken]` | Adds product (trimmed), redirects to `Products` |
| `ProductEdit(int id)` | GET | Loads product into `AdminProductEditVm` |
| `ProductEdit(id, vm)` | POST `[ValidateAntiForgeryToken]` | Mutates tracked entity + saves |
| `ProductDelete(id)` | POST `[ValidateAntiForgeryToken]` | Removes product |

### 4.7 `AdminUsersApiController.cs`

```csharp
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminUsersApiController : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserRowDto>>> GetUsers(CancellationToken ct) { ... }
}
public record AdminUserRowDto(
    string Id, string? Email, string? UserName,
    bool EmailConfirmed, string? PhoneNumber,
    DateTimeOffset? LockoutEnd, bool TwoFactorEnabled);
```
Used by the `Admin/Users` view's client-side table fetch. Unauthenticated API callers get **401** (not a redirect) because of the cookie `OnRedirectToLogin` override.

### 4.8 `LanguageController.cs`

```csharp
[HttpPost, ValidateAntiForgeryToken]
public IActionResult Set(string culture, string returnUrl)
{
    culture = (culture ?? "en").ToLowerInvariant();
    if (culture is not ("en" or "ar")) culture = "en";
    Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions { IsEssential = true, HttpOnly = false, Expires = 1 year });
    if (!Url.IsLocalUrl(returnUrl)) returnUrl = Url.Content("~/");
    return LocalRedirect(returnUrl);
}
```
The navbar's EN/AR toggle posts here with the current path as `returnUrl`.

---

## 5. Web — ViewModels (`PixelAndBit.Web/Models`)

### `CreateBookingVm.cs`
- `[Required]` `CustomerName` (2–150)
- `[Required]` `PhoneNumber` — regex `^07\d{8}$`
- `[Required]` `DeviceType` (default `"Phone"`)
- `[Required]` `DeviceModel` (2–200)
- `[Required]` `IssueDescription` (20–2000)
- `[Range(0, 999999)] decimal? EstimatedCost`

Display names and error messages are **localizer keys** resolved via `SharedResource.resx`.

### `TrackBookingVm.cs`
- `[Required]` `TicketReference` — regex `^PB-\d{4}-[A-Z0-9]{4}$`
- `Booking? Result` populated after a successful lookup

### `AdminDashboardVm.cs`
- Totals: `TotalProducts`, `PendingRepairs`, `TotalRequests`, `TotalUsers`
- Revenues: `ServicesRevenueJod`, `SalesRevenueJod`
- `IReadOnlyList<(string DeviceModel, int Count)> TopDeviceModels`

### `AdminProductEditVm.cs`
- `int Id`
- `[Required, StringLength(120)] string Name`
- `[Range(0, 1_000_000)] decimal Price`
- `[Required, StringLength(2000)] string Description`
- `[Range(0, 1_000_000)] int StockQuantity`
- `[StringLength(500)] string? ImageUrl`

### `ErrorViewModel.cs`
```csharp
public string? RequestId { get; set; }
public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
```

---

## 6. Web — Razor Pages (Identity area)

All pages live under `PixelAndBit.Web/Areas/Identity/Pages/Account/`. `_ViewStart.cshtml` sets `Layout = "/Views/Shared/_Layout.cshtml"`, so the auth pages render under the same navbar/theme as the rest of the site.

### 6.1 `Login.cshtml(.cs)`

- Inputs: `Email`, `Password`, `RememberMe`.
- Shows a **"Forgot password?"** link and — when `AllowPublicRegistration` is true — a **"Create account"** button under the Login button.
- Renders `pb_toast` / `pb_toast_error` (used by the reset flow on return).
- `OnPostAsync`:
  1. Validates.
  2. If the account exists but email is **not** confirmed, redirects to `VerifyEmail` page.
  3. Otherwise calls `SignInManager.PasswordSignInAsync(email, password, remember, lockoutOnFailure: false)`.
  4. On success: `LocalRedirect(returnUrl ?? "~/")`.
  5. On failure: generic `"Invalid login attempt."`.

### 6.2 `Register.cshtml(.cs)`

- Gated by `_configuration.GetValue("AllowPublicRegistration", false)`.
- Creates user with `EmailConfirmed = false`, generates a 5-digit code, hashes it, stores in `EmailVerificationCodes` (10-min TTL, 6 attempts), sends email with `VerifyEmailModel.BuildEmailHtml(code, includeRegistrationWelcome: true)`.
- Redirects to `/Identity/Account/VerifyEmail?email=...`.

### 6.3 `VerifyEmail.cshtml(.cs)`

The richest page.

- Ctor DI: `PixelBitDbContext`, `UserManager<IdentityUser>`, `SignInManager<IdentityUser>`, `IEmailSender`.
- Constants: `ResendCooldownPeriodSeconds = 60`, `MaxResendsPerHour = 10`.
- `OnGet(email, returnUrl)` — bails to `Register` if no email; reads `ResendCooldownSeconds` from the latest code timestamp.
- `OnPostAsync`:
  1. Find user by email; if already confirmed → sign in and redirect.
  2. Get latest **non-consumed, non-expired** code for this user/email.
  3. If none → "Code expired".
  4. If `Attempts >= MaxAttempts` → "Too many attempts".
  5. Increment attempts; `FixedTimeEquals(HashCode(userId, Email, Input.Code), rec.CodeHash)`.
  6. On match: stamp `ConsumedAtUtc`, set `EmailConfirmed = true`, `SignInAsync`, redirect.
- `OnPostResendAsync(email, returnUrl)`:
  - Enforces 60s cooldown on *last* issued code.
  - Enforces ≤ 10 codes / hour per user.
  - Generates + stores + emails a new code.
- Static helpers reused across the app:
  - `Generate5DigitCode()` — `RandomNumberGenerator.GetInt32(10000,100000)`
  - `HashCode(userId, email, code) = SHA256(userId|EMAIL_UPPER|code)` hex
  - `FixedTimeEquals(a,b)` — byte-length guard + `CryptographicOperations.FixedTimeEquals`
  - `BuildEmailHtml(code, includeRegistrationWelcome)` — light, transactional, mobile-friendly HTML (see `03_Frontend.md`).

### 6.4 `Logout.cshtml(.cs)`

- `OnPost` signs out and redirects to `~/`.
- `OnGet` redirects back to login — prevents accidental GET-based logout from CSRF.

### 6.5 `AccessDenied.cshtml(.cs)`

Minimal: sets `ReturnUrl` from query; view offers **Home** / **Sign in** links with the glassy theme.

### 6.6 `RegisterConfirmation.cshtml(.cs)`

Plain confirmation placeholder (not actively used in the post-register flow — we go straight to `VerifyEmail`).

### 6.7 Forgot-password trio (session-backed)

#### `PasswordResetFlow.cs`
A static helper, **no DB writes**:
- Session key constants: `K_Email`, `K_UserId`, `K_CodeHash`, `K_ExpiresTicks`, `K_Attempts`, `K_MaxAttempts`, `K_ResetToken`, `K_Verified`, `K_LastSentTicks`.
- Config: `CodeTtlMinutes = 15`, `ResendCooldownSeconds = 60`, `DefaultMaxAttempts = 6`.
- Helpers:
  - `HasPendingEmail(session)` — gate for Step 2 access.
  - `HasPendingCode(session)` — only true when a real code was issued.
  - `IsCodeVerified(session)` — gate for Step 3 access (verified + token + userId).
  - `ResendCooldownSeconds_(session)` — reads `K_LastSentTicks`.
  - `ClearAll(session)` — wipes all keys.
  - `Generate5DigitCode()` / `HashCode("reset|userId|EMAIL|code")` / `FixedTimeEquals(...)`
  - `BuildResetEmailHtml(code)` — same light Amazon-style HTML as verify.

#### `ForgotPassword.cshtml(.cs)` (Step 1)
- Inputs: `Email`.
- `OnPostAsync`:
  1. `ClearAll(session)`.
  2. **Always** store `email`, `attempts=0`, `maxAttempts`, `lastSentTicks` in session (so Step 2 is reachable regardless of existence).
  3. If the user exists AND has confirmed email:
     - Generate code, call `UserManager.GeneratePasswordResetTokenAsync(user)` (Identity's real token), store code hash + userId + token + 15-min expiry in session, `SendAsync` the email.
  4. Else: small delay to blur timing; no session token/hash stored.
  5. Redirect to Step 2 + toast: *"If an account exists…"*.

#### `VerifyResetCode.cshtml(.cs)` (Step 2)
- `OnGet` — redirects to Step 1 if `!HasPendingEmail`.
- `OnPost`:
  1. Gate on `HasPendingEmail`.
  2. Read `userId/email/codeHash/expTicks/attempts/max`.
  3. If `attempts >= max` → wipe + redirect to Step 1 with toast.
  4. Increment `attempts` **before** comparing (bounded brute-force).
  5. If no `codeHash/userId` (email had no real account) → same generic `"Wrong code. Please try again."` response.
  6. If expired → wipe + redirect to Step 1.
  7. `FixedTimeEquals(HashCode(userId,email,code), codeHash)` — mismatch keeps user on Step 2.
  8. Success: set `K_Verified = "1"`, remove `K_CodeHash` (one-time use), redirect to Step 3.
- `OnPostResendAsync` — always advances cooldown marker (timing parity), re-resolves the user, re-issues code if a real user exists; always toast *"If an account exists…"*.

#### `ResetPassword.cshtml(.cs)` (Step 3)
- `OnGet` / `OnPost` both gate on `IsCodeVerified`. Without it, user is redirected to Step 1 with error toast.
- `OnPost`:
  1. Load `user` by `K_UserId` and `token` by `K_ResetToken`.
  2. `UserManager.ResetPasswordAsync(user, token, Input.Password)`.
  3. `UserManager.UpdateSecurityStampAsync(user)` (invalidates existing sign-ins).
  4. `ClearAll(session)`.
  5. Redirect to Login with success toast.

---

Continue to [`03_Frontend.md`](./03_Frontend.md) for views, layouts, CSS system, and JS.
