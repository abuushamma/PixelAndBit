# 04 — Database, Authentication & Authorization, Configuration

This file covers the data layer, identity/security, and configuration.

---

## 1. Database: `PixelBitDbContext`

Defined in `PixelAndBit.Infrastructure/Data/AppDbContext.cs`:
```csharp
public class PixelBitDbContext : IdentityDbContext<IdentityUser>
{
    public DbSet<Product>                 Products               => Set<Product>();
    public DbSet<Booking>                 Bookings               => Set<Booking>();
    public DbSet<Order>                   Orders                 => Set<Order>();
    public DbSet<OrderItem>               OrderItems             => Set<OrderItem>();
    public DbSet<EmailVerificationCode>   EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<RepairService>           RepairServices         => Set<RepairService>();
    public DbSet<Appointment>             Appointments           => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);                                   // Identity schema
        b.ApplyConfigurationsFromAssembly(typeof(PixelBitDbContext).Assembly);
    }
}
```

- Inherits from `IdentityDbContext<IdentityUser>` which brings the standard ASP.NET Identity 7-table schema.
- `ApplyConfigurationsFromAssembly` auto-registers every `IEntityTypeConfiguration<T>` in `Infrastructure.Data.Configurations/`.

### 1.1 Provider & connection
`Program.cs`:
```csharp
var sqlitePath = Path.Combine(builder.Environment.ContentRootPath, "pixelbit.db");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:PixelBitConnection"] = $"Data Source={sqlitePath}"
});

builder.Services.AddDbContext<PixelBitDbContext>(opts =>
    opts.UseSqlite(sqliteConnectionString,
        sql => sql.MigrationsAssembly("PixelAndBit.Infrastructure")));
```
This makes the database file always live next to the app (beside `Program.cs` in dev; beside the executable in production). The connection string in `appsettings.json` is overridden at startup.

### 1.2 Initialization path
- `EnsureDatabaseInitializedAsync()` runs on first request via a minimal `app.Use(...)` middleware (see `Program.cs`).
- Calls `await db.Database.MigrateAsync()` — applies any pending migrations automatically.
- In **Development** only, calls `DbSeeder.SeedAsync(...)` after migration.

### 1.3 Migrations
Under `PixelAndBit.Infrastructure/Migrations/`:
- `20260418124033_InitialSqlite.cs` — creates everything:
  - **Identity tables**: `AspNetRoles`, `AspNetUsers`, `AspNetRoleClaims`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserRoles`, `AspNetUserTokens`.
  - **Business tables**: see table below.
  - Indexes: unique `Bookings.TicketReference`, unique `Appointments.ConfirmationCode`, composite index on `EmailVerificationCodes(UserId, Email, ConsumedAtUtc, ExpiresAtUtc)`.
- `PixelBitDbContextModelSnapshot.cs` — EF model snapshot.

### 1.4 Schema summary

```
                              ┌─────────────────────┐
                              │     AspNetUsers     │
                              │ Id PK (string)      │
                              │ Email, UserName…    │
                              │ EmailConfirmed      │
                              │ PasswordHash, Stamp │
                              └─────────┬───────────┘
                                        │ (Id referenced by string UserId fields below, not an FK)
                                        │
        ┌───────────────────────────────┼─────────────────────────────────────┐
        │                               │                                     │
        ▼                               ▼                                     ▼
┌───────────────┐             ┌───────────────┐                   ┌───────────────────────┐
│   Bookings    │             │    Orders     │                   │ EmailVerificationCodes│
│ Id Guid PK    │             │ Id Guid PK    │                   │ Id Guid PK            │
│ TicketRef UQ  │             │ UserId (str)  │                   │ UserId                │
│ UserId (str)? │             │ OrderDate     │                   │ Email                 │
│ Customer*     │             │ TotalAmount   │                   │ CodeHash              │
│ Phone (^07…)  │             │ Status(str)   │                   │ CreatedAtUtc          │
│ DeviceModel   │             └─────┬─────────┘                   │ ExpiresAtUtc          │
│ IssueDesc     │                   │ 1..N                        │ Attempts / MaxAttempts│
│ Status(str)   │                   ▼                             │ ConsumedAtUtc (null)  │
│ CreatedAt     │             ┌───────────────┐                   └───────────────────────┘
│ EstimatedCost │             │  OrderItems   │
└───────────────┘             │ Id Guid PK    │
                              │ OrderId FK    │            ┌──────────────────┐
                              │ ProductId FK  │──────▶     │     Products     │
                              │ Quantity      │            │ Id int PK        │
                              │ UnitPrice     │            │ Name, Desc       │
                              └───────────────┘            │ Price dec(10,2)  │
                                                           │ StockQuantity    │
                                                           │ ImageUrl         │
                                                           └──────────────────┘

┌──────────────────────┐                            ┌──────────────────────┐
│   RepairServices     │                            │    Appointments      │
│ Id int PK            │◀──────────────────────────┤│ Id int PK            │
│ Name                 │                            │ ConfirmationCode UQ  │
│ Description          │                            │ CustomerName/Email… │
│ BasePrice dec(18,2)  │                            │ AppointmentDate      │
│ DurationMinutes      │                            │ StartTime / EndTime  │
│ IsActive             │                            │ Status(str)          │
└──────────────────────┘                            │ RepairServiceId FK   │
                                                    │ Notes / CreatedAt    │
                                                    └──────────────────────┘
```

### 1.5 Per-table highlights (from Fluent configs)

| Table | Key | Important constraints |
|---|---|---|
| `Products` | `Id` int identity | `Name` required ≤200, `Description` required ≤2000, `Price decimal(10,2)`, `ImageUrl` ≤500 |
| `Bookings` | `Id` Guid | `TicketReference` unique ≤20, `UserId` ≤450 nullable, `Phone` ≤20, `DeviceModel` ≤200, `IssueDescription` ≤2000, `Status` stored as string ≤30, `EstimatedCost decimal(10,2)` |
| `Orders` | `Id` Guid | `UserId` ≤450 nullable, `TotalAmount decimal(10,2)`, `Status` stored as string ≤20, `HasMany(Items).OnDelete(Cascade)` |
| `OrderItems` | `Id` Guid | `UnitPrice decimal(10,2)`, `HasOne(Product).OnDelete(Restrict)` → prevents deleting products that are part of an order |
| `Appointments` | `Id` int | `ConfirmationCode` unique ≤20, `Status` stored as string ≤20, `HasOne(RepairService).OnDelete(Restrict)` |
| `RepairServices` | `Id` int | `BasePrice decimal(18,2)` |
| `EmailVerificationCodes` | `Id` Guid | Index on `(UserId, Email, ConsumedAtUtc, ExpiresAtUtc)` |

### 1.6 Relationships at a glance

- `Order 1..N OrderItem` — cascade delete from `Order`. Items removed with the order.
- `OrderItem N..1 Product` — restrict delete. You can't delete a product if it's in an order.
- `Appointment N..1 RepairService` — restrict delete.
- `IdentityUser → Bookings / Orders / EmailVerificationCodes` — *no enforced FK*. Stored as `string UserId` only. This allows guest bookings/orders (null `UserId`) and avoids EF navigating back to the Identity tables by accident.
- No explicit user table for application-level profiles — we rely on Identity's `AspNetUsers`.

### 1.7 Where raw data comes from
- **Seed** (`DbSeeder.cs`, Development only):
  - Roles `Admin`, `Customer`.
  - Admin user `admin@pixelbit.jo` / `Admin@Pb2026!` (`EmailConfirmed=true`).
  - 5 seed products (thermal paste / keyboard modding / console cleaning / HDMI mod / entry-level build).
- **Runtime** — everything else is user-generated via the web UI / APIs.

### 1.8 Health endpoint
`GET /__health/db` returns `{ ok: true, products: <count>, database: "sqlite", file: "<path>" }` — useful for load balancer probes.

---

## 2. Authentication

### 2.1 Configuration (`Program.cs`)
```csharp
builder.Services.AddIdentity<IdentityUser, IdentityRole>(o =>
{
    o.Password.RequireDigit            = false;
    o.Password.RequiredLength          = 6;
    o.Password.RequireNonAlphanumeric  = false;
    o.Password.RequireUppercase        = false;
    o.SignIn.RequireConfirmedAccount   = true;  // blocks login until email is verified
})
.AddEntityFrameworkStores<PixelBitDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath        = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = 401;      // APIs → proper status code
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = 403;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        }
    };
});
```

- **Password policy** is intentionally relaxed (length 6, no uppercase/digit required) to reduce friction; raise these in production if needed.
- `SignIn.RequireConfirmedAccount = true` is what makes the custom **5-digit email verification** mandatory.
- Cookie events convert redirects to 401/403 for `/api/*` paths — critical for the admin users JSON API.

### 2.2 Registration + email verification (custom 5-digit flow)

Files: `Areas/Identity/Pages/Account/Register.cshtml.cs`, `VerifyEmail.cshtml.cs`.

1. `Register.OnPostAsync` creates the `IdentityUser` with `EmailConfirmed=false`.
2. Generates a 5-digit code, hashes it `SHA256(userId|EMAIL_UPPER|code)`, stores it in `EmailVerificationCodes` (expiry 10 min, 6 attempts).
3. Sends the code via `IEmailSender.SendAsync` using the clean light HTML from `VerifyEmailModel.BuildEmailHtml(code, includeRegistrationWelcome: true)`.
4. Redirects to `/Identity/Account/VerifyEmail?email=...`.
5. User enters code → compared in constant time → sets `user.EmailConfirmed = true` → `SignInAsync`.

Rate-limits on verification:
- **Resend cooldown**: 60 seconds since the last code.
- **Hourly cap**: 10 codes per user per hour.

### 2.3 Login

File: `Login.cshtml.cs`.

```csharp
var user = await _userManager.FindByEmailAsync(Input.Email);
if (user != null && !await _userManager.IsEmailConfirmedAsync(user))
    return RedirectToPage("./VerifyEmail", new { email = Input.Email, returnUrl });

var result = await _signInManager.PasswordSignInAsync(
    Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
```
Same form serves users and admins. Role-based authorization picks up once they sign in.

### 2.4 Forgot password (multi-step)

Files: `ForgotPassword.cshtml.cs`, `VerifyResetCode.cshtml.cs`, `ResetPassword.cshtml.cs`, `PasswordResetFlow.cs`.

- State lives in **server session** (no DB). Keys are prefixed `pb.reset.*`.
- Step 1 always stores the submitted email (even when no account exists) so Step 2 is always reachable. A real code + Identity reset token are only stored when the account is real and email-confirmed.
- Step 2 enforces:
  - `HasPendingEmail` gate
  - attempt counter increment **before** comparison
  - `codeHash/userId` presence check → same generic "Wrong code" response when absent (no info leak)
  - expiry check (15 min)
  - `CryptographicOperations.FixedTimeEquals` for constant-time compare
  - one-time use (code hash removed on success)
- Step 3 gated on `IsCodeVerified` (verified flag + token + userId). Uses Identity's own `UserManager.ResetPasswordAsync(user, token, newPw)` + `UpdateSecurityStampAsync` to invalidate existing sign-in cookies.

### 2.5 Logout

`Logout.cshtml.cs`:
- `OnPost` → `SignOutAsync` → redirect to `~/`.
- `OnGet` → redirect to login (prevents CSRF via GET).

Navbar renders both the desktop and mobile logout as real POST forms with antiforgery tokens.

### 2.6 Where is the admin link in the navbar?

`Views/Shared/_Navbar.cshtml`:
```razor
@if (User.IsInRole("Admin"))
{
    <li class="nav-item">
        <a asp-controller="Admin" asp-action="Index">@T["Nav.Admin"]</a>
    </li>
}
```
The Admin link is therefore hidden entirely from the public (including anonymous users). Admin acquires visibility of admin features only after signing in.

---

## 3. Authorization

### 3.1 Role-based (`Admin`)

Applied at controller class level where appropriate:

| Target | Attribute |
|---|---|
| `AdminController` | `[Authorize(Roles = "Admin")]` |
| `AdminUsersApiController` | `[Authorize(Roles = "Admin")]` + `[ApiController]` + `[Route("api/admin")]` |
| `BookingController.Admin`, `BookingController.UpdateStatus` | `[Authorize(Roles = "Admin")]` on those specific actions |

### 3.2 Authenticated-only

- `ProfileController` — `[Authorize]` at class level (any signed-in user).

### 3.3 Anti-forgery (CSRF)

- Applied to every mutating POST via `[ValidateAntiForgeryToken]` or implicit binding via `asp-for` forms.
- JS-initiated POSTs pass `window.__pb.csrf` in the `RequestVerificationToken` header (see `_Layout.cshtml` + `site.js`).

---

## 4. Configuration

### 4.1 `appsettings.json`
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AllowPublicRegistration": true,
  "ConnectionStrings": {
    "PixelBitConnection": "Data Source=pixelbit.db"
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 465,
    "EnableSsl": true,
    "Username": "pixelbitjo@gmail.com",
    "Password": "mzfpqidcqozmugdx",      // Gmail app password
    "FromEmail": "pixelbitjo@gmail.com",
    "FromName": "Pixel & Bit"
  }
}
```

Important keys:
- `AllowPublicRegistration` — when false, `/Identity/Account/Register` redirects back to login with a toast.
- `ConnectionStrings:PixelBitConnection` — overwritten by `Program.cs` at boot with the absolute SQLite path.
- `Smtp` — bound to `SmtpEmailOptions`; if not properly configured (placeholders like `YOUR_HOST`), `NullEmailSender` is registered instead.

> ⚠️ **Secret**: the current `Smtp.Password` is a Gmail app password checked into source. In production, move this to environment variables / user secrets / a key vault.

### 4.2 `appsettings.Development.json`
```json
{
  "AllowPublicRegistration": true,
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "ConnectionStrings": { "PixelBitConnection": "Data Source=pixelbit.db" }
}
```

### 4.3 `appsettings.Production.json`
Same as base, with `LogLevel.Default: Warning` and the same `AllowPublicRegistration: true`.

### 4.4 Environment variables of interest
- `ASPNETCORE_ENVIRONMENT` = `Development` | `Production`
- `ASPNETCORE_URLS` = e.g. `http://127.0.0.1:5001` (overrides `Program.cs` defaults)

---

## 5. SMTP delivery

Implementation: `PixelAndBit.Infrastructure/Email/SmtpEmailSender.cs` using **MailKit / MimeKit**.

- Defaults: `smtp.gmail.com:465` with implicit SSL.
- Uses app-password authentication (Gmail).
- `BuildResetEmailHtml` / `BuildEmailHtml` produce a light, table-based, inline-styled, mobile-friendly HTML that renders reliably in Gmail/Outlook/Apple Mail.
- Logs to stdout for debugging (`=== SMTP DEBUG START / END / ERROR ===`).

Callers handle failures defensively:
- `Register` / `ForgotPassword` / `Resend` wrap in `try/catch`, **never** surface delivery errors to the user (to avoid leaking account existence).
- `VerifyEmailModel.OnPostResend` exposes a generic "we couldn't send the email right now" when the user explicitly asked for a resend (that request isn't about existence leakage).

---

## 6. Diagnostic endpoints

| Path | Purpose |
|---|---|
| `GET /__health/db` | JSON: `{ ok, products, database:"sqlite", file }` |
| `GET /Home/Error` | Friendly error page when unhandled exceptions bubble up in Production |

---

Continue to [`05_Deployment_and_Study.md`](./05_Deployment_and_Study.md).
