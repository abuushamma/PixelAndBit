# Pixel & Bit — Technical Documentation

> This document is the single entry point into the project. It points to five focused
> files under `/docs/` that together cover everything: architecture, every backend
> class, every Razor view/page, the database schema, authentication, deployment,
> and a study plan.

**Product**: Pixel & Bit — a tech-repair shop website in Jordan. It offers:
- Device repair **booking** with a public **ticket tracking** system (e.g. `PB-2026-1A2B`)
- An **online store** (service/product catalog) with a **session-based cart** and simple checkout
- An **admin dashboard** (products, orders, bookings, users)
- **ASP.NET Core Identity** authentication with a custom **5-digit email verification** flow and a custom **multi-step forgot-password** flow
- **Bilingual UI** (English / Arabic RTL) via `IStringLocalizer` + a cookie-based culture switcher

**Stack**
- **.NET 8** (ASP.NET Core MVC + Razor Pages Identity UI)
- **Entity Framework Core 8** with **SQLite** (file: `pixelbit.db`)
- **ASP.NET Core Identity** (`IdentityUser`, `IdentityRole`)
- **MailKit / MimeKit** for SMTP email
- **Bootstrap 5** + custom `pixelbit.css` (dark premium theme, gradients, glass)
- Session-backed cart + password-reset state; distributed memory cache

**Solution layout** (4 projects, Clean-Architecture-ish):
```
PixelAndBit.sln
├── PixelAndBit.Domain           ← entities + enums (no dependencies)
├── PixelAndBit.Application      ← service interfaces + DTOs
├── PixelAndBit.Infrastructure   ← EF Core, DbContext, services, email, migrations
└── PixelAndBit.Web              ← MVC controllers, Razor Pages, views, wwwroot
```

---

## Documentation map

| # | File | What it covers |
|---|---|---|
| 1 | [`01_Overview.md`](./01_Overview.md) | Project overview, architecture, folder tree, request flow, `Program.cs` walkthrough |
| 2 | [`02_Backend.md`](./02_Backend.md) | Every controller + action, every service, every interface, every entity/enum/view-model, Identity pages |
| 3 | [`03_Frontend.md`](./03_Frontend.md) | Razor views, layout, navbar, auth UI, CSS design system, `site.js`, localization + RTL |
| 4 | [`04_Database_Auth_Config.md`](./04_Database_Auth_Config.md) | `PixelBitDbContext`, entity configurations, migrations, schema tables, auth/authorization, `appsettings`, SMTP |
| 5 | [`05_Deployment_and_Study.md`](./05_Deployment_and_Study.md) | Running locally, publishing, Linux/Kestrel/systemd/Nginx patterns, study plan, risks & improvements |

---

## Quick start (TL;DR)

```powershell
# From the repo root, in a dedicated terminal:
./scripts/start-dev.ps1
# → http://localhost:5001
```

Development seeds an admin: `admin@pixelbit.jo` / `Admin@Pb2026!`
(see `PixelAndBit.Infrastructure/Data/DbSeeder.cs`).

---

Documentation file created at: `/docs/PixelAndBit_Documentation.md`
