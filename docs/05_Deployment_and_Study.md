# 05 — Deployment, Study Plan, Risks, and Final Summary

---

## 1. Running locally

### 1.1 Pre-requisites
- **.NET 8 SDK** — `dotnet --version` should print `8.x`.
- Windows, macOS, or Linux.
- No separate DB server — SQLite is file-based (`pixelbit.db`).

### 1.2 Start with hot reload
```powershell
# From the repo root, in a dedicated terminal window:
./scripts/start-dev.ps1
```
This runs `dotnet watch run --project "PixelAndBit.Web/PixelAndBit.Web.csproj"`. Keep the window open; stop with `Ctrl+C`.

- Dev binding: `http://0.0.0.0:5001` (LAN testing from your phone works).
- Browse: `http://localhost:5001`.
- Default seeded admin: `admin@pixelbit.jo` / `Admin@Pb2026!`.

### 1.3 Reset local DB
```powershell
# Stop the app first if running.
Remove-Item PixelAndBit.Web/pixelbit.db -Force
Remove-Item PixelAndBit.Web/pixelbit.db-wal -ErrorAction SilentlyContinue
Remove-Item PixelAndBit.Web/pixelbit.db-shm -ErrorAction SilentlyContinue
# Next run will re-migrate and re-seed.
```

### 1.4 Add a new migration
```bash
cd PixelAndBit.Web
dotnet ef migrations add <Name> --project ../PixelAndBit.Infrastructure --startup-project .
dotnet ef database update --project ../PixelAndBit.Infrastructure --startup-project .
```
Migrations live in `PixelAndBit.Infrastructure/Migrations/`. Program startup also auto-applies them on first request.

---

## 2. Publishing

### 2.1 Framework-dependent publish
```bash
dotnet publish PixelAndBit.Web/PixelAndBit.Web.csproj -c Release -o publish
```
This writes the app + assets to `./publish/`. You can copy that folder to any Linux/Windows host with the matching .NET 8 runtime installed.

### 2.2 Self-contained publish (no .NET install required on the host)
```bash
dotnet publish PixelAndBit.Web/PixelAndBit.Web.csproj -c Release -r linux-x64 --self-contained true -o publish-linux
```

### 2.3 What ends up in publish?
- `PixelAndBit.Web.dll` + dependencies
- `wwwroot/` assets (favicon, logo, `pixelbit.css`, `site.js`, libs)
- `appsettings*.json` — provide `appsettings.Production.json` on the server with real SMTP credentials.
- `pixelbit.db` — usually **do not** ship; let the app create a fresh file on first run (it'll migrate and — only in Development — seed).

---

## 3. Linux host pattern (Kestrel + systemd + Nginx)

This project targets a reverse-proxy deployment. The Kestrel port in Production defaults to `127.0.0.1:5001` (see `Program.cs`), so it's only reachable via the reverse proxy.

> There is no `.service`, `nginx.conf`, or `Dockerfile` shipped in the repo. The snippets below are the recommended shape based on the project's runtime behavior.

### 3.1 systemd unit (example)
`/etc/systemd/system/pixelbit.service`
```ini
[Unit]
Description=Pixel & Bit web app (ASP.NET Core 8)
After=network.target

[Service]
WorkingDirectory=/var/www/pixelbit
ExecStart=/usr/bin/dotnet /var/www/pixelbit/PixelAndBit.Web.dll
Restart=always
RestartSec=10
SyslogIdentifier=pixelbit
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
# Explicit override (already the default in Program.cs):
Environment=ASPNETCORE_URLS=http://127.0.0.1:5001

# Secrets via env, not appsettings.json:
Environment=Smtp__Host=smtp.gmail.com
Environment=Smtp__Port=465
Environment=Smtp__EnableSsl=true
Environment=Smtp__Username=YOUR_USER
Environment=Smtp__Password=YOUR_APP_PASSWORD
Environment=Smtp__FromEmail=YOUR_USER
Environment=Smtp__FromName=Pixel & Bit

[Install]
WantedBy=multi-user.target
```
Operate:
```bash
sudo systemctl daemon-reload
sudo systemctl enable --now pixelbit
sudo systemctl status pixelbit
journalctl -u pixelbit -f
```

### 3.2 Nginx (TLS-terminating reverse proxy)
`/etc/nginx/sites-available/pixelbit`
```nginx
server {
    listen 80;
    server_name pixelbit.example.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name pixelbit.example.com;

    ssl_certificate     /etc/letsencrypt/live/pixelbit.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/pixelbit.example.com/privkey.pem;

    # Big-enough for HTML email templates (just in case)
    client_max_body_size 10m;

    location / {
        proxy_pass         http://127.0.0.1:5001;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   Upgrade           $http_upgrade;   # WebSockets (future-proof)
        proxy_set_header   Connection        keep-alive;
    }
}
```
Enable + reload:
```bash
sudo ln -s /etc/nginx/sites-available/pixelbit /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

### 3.3 Forwarded headers (optional but recommended)
If you rely on `HttpContext.Request.Scheme == "https"` or `RemoteIpAddress` in code or logging, add to `Program.cs` before `UseAuthentication`:
```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```
This is **not** currently wired in `Program.cs` — consider adding it when you move to production.

### 3.4 Persistence of SQLite
- Keep `pixelbit.db` in a writable directory owned by the service user (e.g., `/var/www/pixelbit/`).
- Back it up periodically (`sqlite3 pixelbit.db ".backup /var/backups/pixelbit-$(date +%F).db"`).
- Prefer keeping WAL journal on (`pragma journal_mode = wal;` default).

---

## 4. Windows hosting (optional)

You can also host on Windows via **IIS + AspNetCoreModuleV2**:
- Publish the app.
- Configure an IIS site pointing to the publish folder.
- Install the **.NET 8 Hosting Bundle**.
- Make the app pool run with a user that can write the SQLite file.
- Set environment variables via the IIS Configuration Editor (same names as above, e.g. `ASPNETCORE_ENVIRONMENT=Production`).

---

## 5. Study plan — how to read this project

A step-by-step order that matches how the app is wired.

### Step 1 — Orient yourself
1. Open `PixelAndBit.sln` to see the four projects.
2. Read **`docs/01_Overview.md`** — gives you the mental model.
3. Open `PixelAndBit.Web/Program.cs` and skim. Spot:
   - SQLite path override
   - Identity config (password + `RequireConfirmedAccount`)
   - Session + distributed cache
   - Cookie events redirecting API calls to 401/403
   - `EnsureDatabaseInitializedAsync` pattern
   - Routing order

### Step 2 — Domain & Application
1. `PixelAndBit.Domain/Entities/*.cs` — 7 files, ~100 lines each.
2. `PixelAndBit.Domain/Enums/*.cs` — 4 tiny enums.
3. `PixelAndBit.Application/Interfaces/*.cs` — the service contracts.

### Step 3 — Infrastructure
1. `Data/AppDbContext.cs` — inherits `IdentityDbContext<IdentityUser>`.
2. `Data/Configurations/*.cs` — learn how Fluent API shapes each table.
3. `Migrations/20260418124033_InitialSqlite.cs` — how it becomes SQL.
4. `Data/DbSeeder.cs` — roles + admin + default products.
5. `Data/ProductService.cs`, `BookingService.cs`, `CartService.cs`, `AppointmentService.cs` — implementations.
6. `Email/SmtpEmailSender.cs` + `NullEmailSender.cs`.

### Step 4 — Web layer (the fun part)
1. `Controllers/HomeController.cs` (simplest).
2. `Controllers/StoreController.cs` (search + catalog).
3. `Controllers/BookingController.cs` (booking, tracking, admin status change).
4. `Controllers/CartController.cs` (session cart + checkout).
5. `Controllers/ProfileController.cs` (authenticated user history).
6. `Controllers/AdminController.cs` (dashboard math: `SumAsync<double?>` cast to decimal — read the comment above it).
7. `Controllers/AdminUsersApiController.cs` (JSON API).
8. `Controllers/LanguageController.cs` (culture cookie).

### Step 5 — Identity
1. `Areas/Identity/Pages/Account/Login.cshtml(.cs)` — shared login.
2. `Register.cshtml(.cs)` + `VerifyEmail.cshtml(.cs)` — 5-digit email verify flow.
3. `PasswordResetFlow.cs` — session keys + helpers.
4. `ForgotPassword / VerifyResetCode / ResetPassword` — Steps 1→2→3.
5. `Logout.cshtml.cs` — POST-only safety.

### Step 6 — UI
1. `Views/Shared/_Layout.cshtml` — the backbone.
2. `Views/Shared/_Navbar.cshtml` — active-link tracking, role-aware, cart badge, EN/AR toggle, offcanvas.
3. `Views/Home/Index.cshtml` — the biggest homepage view.
4. `Views/Booking/*`, `Views/Store/Index.cshtml`, `Views/Cart/*`, `Views/Admin/*`, `Views/Profile/*`.
5. `wwwroot/css/pixelbit.css` — search for the groups documented in `03_Frontend.md`.
6. `wwwroot/js/site.js` — short and worth reading end to end.

### Step 7 — Localization
1. `Resources/SharedResource.cs` (the marker class).
2. `Resources/SharedResource.resx` / `.ar.resx` — same keys in both files.
3. `LanguageController.cs` + the lang forms in the navbar.

### Step 8 — End-to-end smoke test
1. Register a new account; copy the code from the inbox; verify; you're logged in.
2. Book a repair; note the `PB-2026-XXXX` ticket; sign out; track the ticket anonymously.
3. Add a store item, check the badge updates (AJAX), confirm checkout.
4. Sign in as `admin@pixelbit.jo`; move a booking to `ReadyForPickup`; mark an order `Completed`; watch the dashboard numbers move.
5. Do a "Forgot password" flow; ensure the 3-step gating works (direct-hit Step 2 / Step 3 URLs redirect you back correctly).

---

## 6. Problems / Risks / Improvements

This section is honest about what could bite you.

### 6.1 Security
- **Secrets in source control** — `appsettings.json` contains a real Gmail app password. Remove it, use environment variables / `dotnet user-secrets` in dev, and a secrets manager in prod.
- **Seeded admin password** — `Admin@Pb2026!` is hardcoded. Change after first deploy, or gate seeding behind a first-run flag.
- **Password policy** is relaxed (6 chars, no digits). Tighten for production: `RequireDigit = true`, `RequireNonAlphanumeric = true`, and consider raising `RequiredLength` to 8.
- **No HTTPS redirection** is enabled. Add `app.UseHttpsRedirection()` if the app is ever exposed directly. Behind Nginx with HSTS, this is already covered.
- **Forwarded headers** not wired — logging IPs and issuing secure cookies behind Nginx relies on `X-Forwarded-*`.
- **CSRF on AJAX** works because of `window.__pb.csrf`. Anyone adding new fetch calls must remember to forward the `RequestVerificationToken` header.
- **Role claims cache** — when you add/remove a user from a role, existing cookie sessions keep the old role until they refresh / log out. Consider `UpdateSecurityStampAsync` after role changes too.

### 6.2 Data & reliability
- **SQLite** is fine for a single instance but not ideal for scale-out. Behavior under very high concurrency (and especially inside OneDrive) is risky — keep the `pixelbit.db` file on a local volume in production.
- **No FK from `UserId` string columns to `AspNetUsers.Id`** — by design, to allow guest rows. But it also means deleting a user does **not** remove their historical bookings/orders. That might be desirable (audit) or not (privacy).
- **No transaction in `Cart.Confirm`** — inserting `Order` and clearing the session cart are not atomic. If the app crashes between them, the cart persists but the order is already saved. Low risk but easy to harden with a transaction.

### 6.3 Email
- Password in `appsettings.json` — see secrets note above.
- `SmtpEmailSender` hardcodes `client.ConnectAsync(host, port, useSsl: true)` (implicit SSL). If you ever switch to `starttls` on port 587, this line needs changing.
- Stdout `Console.WriteLine("SMTP DEBUG …")` leaks the From address and auth username to logs. Consider a log-level guard or structured logging.

### 6.4 Performance
- `StoreController` filters in memory after `ToList` — fine for the current 5-product catalog, not fine at 1k+. Move the `Contains` filter into the query for scale.
- `AdminController.Index` sums over `(double?)` to dodge the EF Core SQLite decimal-sum limitation. Comment is in the code; document it if the provider changes.
- Homepage loads 4 font families and several Google Fonts subsets. Consider local hosting or `font-display: swap` optimization.

### 6.5 UX / Accessibility
- Buttons/inputs use `pb-input` / `pb-btn-glow`; focus-visible states exist but verify contrast on light OS themes.
- Color-only cues in status badges — pair with icon/text for accessibility.
- Provide `aria-live` on the cart badge so screen readers announce updates.

### 6.6 Feature gaps
- `AppointmentService` — `GetAvailableDatesAsync` / `GetAvailableSlotsAsync` are stubs returning empty. Slot-based booking UI isn't wired to the user-facing site yet.
- No admin UI to manually confirm someone's email or resend a code.
- No paginated/search-able admin `Users` view (the JSON API just returns everything).
- Password strength meter, "show password" toggle, and 2FA enrollment are all missing.

### 6.7 Testability
- No test project exists. The cleanest candidates to add are:
  - `BookingServiceTests` — phone regex, ticket generation collisions, status transitions.
  - `CartServiceTests` — add/remove/clear semantics over a fake session.
  - `PasswordResetFlowTests` — hashing, cooldown math, session gating.

### 6.8 Operations
- Dev server prefers `0.0.0.0:5001`; production prefers `127.0.0.1:5001`. Document this clearly in ops runbooks — several engineers stumble on "why can't I reach it externally" on production without a reverse proxy.
- Add a CI workflow running `dotnet build` + `dotnet test` before merging to main.
- A `Dockerfile` + `docker-compose.yml` would make onboarding faster; SQLite volume mount is straightforward.

---

## 7. Final summary

- The app is a **well-layered ASP.NET Core 8 application** following a clean architecture with four projects: `Domain`, `Application`, `Infrastructure`, `Web`.
- It combines **MVC controllers** for business pages with **Razor Pages (Identity)** for auth, under a **single shared layout** — the UI looks consistent everywhere.
- **Business features**: store with session cart + guest checkout; repair booking with Jordanian-phone validation and a collision-resistant ticket generator; public ticket tracking; admin dashboard with revenue math and status transitions.
- **Auth**: ASP.NET Core Identity with a **custom 5-digit email verification** code (persisted, hashed, rate-limited) and a **session-backed multi-step forgot-password flow** that reuses Identity's own reset token for the actual password mutation.
- **I18n**: cookie-based culture, `en`/`ar` resources, full RTL via Bootstrap RTL + `.pb-rtl` overrides.
- **Styling**: one large `pixelbit.css` defines the premium dark theme, glassmorphism surfaces, gradients, and motion system; one small `site.js` drives fade-ins, parallax variables, device chips, and cart AJAX.
- **Deployment**: SQLite on a local volume; Kestrel bound to loopback; Nginx as TLS-terminating reverse proxy; systemd for lifecycle.
- **Improvements worth prioritising**: pull secrets out of source, rotate the seeded admin password, tighten password policy for production, add forwarded-headers + HTTPS redirection, and add a small test project.

---

Documentation file created at: `/docs/PixelAndBit_Documentation.md`

Individual sections are at:
- `/docs/01_Overview.md`
- `/docs/02_Backend.md`
- `/docs/03_Frontend.md`
- `/docs/04_Database_Auth_Config.md`
- `/docs/05_Deployment_and_Study.md`
