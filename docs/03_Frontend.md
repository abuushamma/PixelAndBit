# 03 — Frontend: Views, Layout, Styling, JS, Localization

This file documents the UI layer: Razor views, the shared layout and navbar, the design system in `pixelbit.css`, `site.js`, and the localization/RTL pipeline.

---

## 1. Razor infrastructure

### `Views/_ViewImports.cshtml`
```razor
@using PixelAndBit.Web
@using PixelAndBit.Web.Models
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```
Enables `asp-*` tag helpers and imports your namespaces globally.

### `Views/_ViewStart.cshtml`
```razor
@{
    Layout = "_Layout";
}
```
Applies `_Layout.cshtml` to every MVC view. The Razor Pages Identity pages have their own `_ViewStart` under `Areas/Identity/Pages/` that **also** points to `/Views/Shared/_Layout.cshtml` — guaranteeing a single consistent layout for the entire site.

### `Views/Shared/_Layout.cshtml`
- Sets `<html lang="@lang" dir="@(isAr ? "rtl" : "ltr")">`.
- Preconnects Google Fonts and loads 4 families: **Inter** (body), **Space Grotesk** (headings), **Orbitron** (brand/ticket), **Cairo** (Arabic heading).
- Picks the Bootstrap bundle:
  ```razor
  @if (isAr)  <link href="~/lib/bootstrap/.../bootstrap.rtl.min.css" />
  @else       <link href="~/lib/bootstrap/.../bootstrap.min.css" />
  ```
- Loads `site.css`, `pixelbit.css`, and the per-project scoped CSS.
- `<body class="@(isAr ? "pb-rtl" : "pb-ltr") pb-has-fixed-header">`.
- Fixed header wraps `<partial name="_Navbar">`.
- `<main id="main-content">` + content container with `pb-section pb-fade`.
- Footer: brand, Google Maps link, WhatsApp link, Instagram link — all localized.
- Injects a CSRF token to JS:
  ```html
  <script>
      window.__pb = window.__pb || {};
      window.__pb.csrf = @Html.Raw(JsonSerializer.Serialize(Antiforgery.GetAndStoreTokens(Context).RequestToken));
  </script>
  ```
  `site.js` and any other AJAX send this token back via the `RequestVerificationToken` header — that's how `[ValidateAntiForgeryToken]` on APIs like `Cart/Add` is satisfied without `<form>` POSTs.

### `Views/Shared/_Navbar.cshtml`
- Active-link tracking: inspects `ViewContext.RouteData.Values["controller"/"action"]`.
- **Live cart badge**: reads `await CartService.GetItemCountAsync()` on every render; wrapped in a `pb-cart-badge` element with id `pb-cart-badge` updated via JS after Add/Remove.
- **Language toggle** (`<form method="post" asp-controller="Language">`), separate compact version for mobile top-right cluster.
- **Role-aware admin link**:
  ```razor
  @if (User.IsInRole("Admin")) { ...Admin link... }
  ```
- **Auth actions**:
  - When signed in → "Profile" link + logout form (POST, CSRF-safe).
  - When anonymous → "Sign in" link to `~/Identity/Account/Login`.
- **Mobile drawer** (`offcanvas`): mirrors all nav links + auth actions with the same role checks.
- Mobile hamburger button triggers `#pbMobileNav`; cart button also shown on small screens so users always see their cart.

### `Views/Shared/Error.cshtml`
Minimal error view; renders `RequestId` when present.

### `Views/Shared/_ValidationScriptsPartial.cshtml`
Includes jquery-validation + jquery-validation-unobtrusive so `asp-validation-for` / `asp-validation-summary` show real-time client validation.

---

## 2. Page views (MVC)

### 2.1 `Home/Index.cshtml`
Marketing homepage. Key sections (each wrapped in `.pb-saas-section`):
- **Hero** (`.pb-hero2`) — aurora, grid, sparks, CTA buttons ("Book repair" / "Track ticket" / anchor link).
- **Benefits strip** (`.pb-benefits-strip`) — 3 icon cards (Precise Diagnosis / Quality Repair / Quick Service).
- **Value** — 3 cards (Fast booking / Transparent process / Track anytime).
- **Features** — 2 tall cards (included-with-service tag grid + Store CTA).
- **Services** — 3 service cards linking to Booking / Store / Track.
- **Work** — Instagram-linked gallery thumbnails.
- **Actions** (*Get Started Instantly*) — 3 equal-height cards with identical full-width CTAs (`.pb-action-btn`).
- **Visit** — full-bleed gradient section with a Google Maps CTA.

All grid columns use `col-12 col-md-6 col-lg-4 d-flex` + `w-100 d-flex flex-column` pattern so cards stretch equal-height and CTAs sit at the bottom via `mt-auto`.

### 2.2 `Home/Contact.cshtml`, `Home/Privacy.cshtml`
Static informational pages.

### 2.3 `Store/Index.cshtml`
- Store brand header, search form (`<form method="get">?q=`).
- Product grid with **card flex-column** and CTA at bottom.
- **Add-to-cart** is AJAX (`POST /Cart/Add` with `RequestVerificationToken` header); response updates `#pb-cart-count`.

### 2.4 `Booking/Index.cshtml`
- Bound to `CreateBookingVm`.
- **Device chips** (`.pb-chip`) — JS toggles `is-active` and writes the value into a hidden `<input asp-for="DeviceType">`.
- Phone input uses `pb-ltr-inline` so numbers stay LTR inside an Arabic RTL page.
- Submit button: full-width `.pb-btn-glow` on the primary gradient.

### 2.5 `Booking/Track.cshtml`
- `TrackBookingVm` + small lookup form.
- After lookup, renders the ticket reference (Orbitron font) and a status `badge`.

### 2.6 `Booking/Success.cshtml`
- Displays `ViewBag.Ticket` prominently with copy-to-clipboard friendly formatting.

### 2.7 `Booking/Admin.cshtml`
- Admin-only list of bookings with search, status dropdown per row. Each dropdown change fires a fetch to `Booking/UpdateStatus` with the CSRF header.

### 2.8 `Cart/Index.cshtml`
- List of `CartLine` (product + qty + subtotal).
- "Clear cart" POST form; "Confirm" POST form; both CSRF-protected.
- Shows `ViewBag.Total` with thousands separator.

### 2.9 `Cart/Success.cshtml`
- Order detail view. Items listed with unit price and line totals.

### 2.10 `Profile/Index.cshtml`, `Profile/MyOrders.cshtml`, `Profile/MyRepairs.cshtml`
- Landing + two list views for the signed-in user's history.

### 2.11 Admin views

| View | Purpose |
|---|---|
| `Admin/Index.cshtml` | Dashboard cards (counts, revenue, top-8 device models with progress bars) + quick-action buttons |
| `Admin/Users.cshtml` | Users table — populated client-side via `GET /api/admin/users` (JSON) |
| `Admin/Products.cshtml` | Products list + CRUD links |
| `Admin/ProductCreate.cshtml` / `ProductEdit.cshtml` | Bound to `AdminProductEditVm` |
| `Admin/Orders.cshtml` | All orders list |
| `Admin/OrderDetails.cshtml` | Single order + line items + status update |

---

## 3. Identity views (Razor Pages)

All under `Areas/Identity/Pages/Account/*.cshtml`. They share:
- `.pb-auth-shell` — centered, max-width, responsive container
- `.pb-auth-card` — the dark glassy card with blurred backdrop
- `.pb-auth-btn` — 46 px min-height, 14 px radius, consistent typography
- `.pb-auth-link` — muted text-like link style
- `.pb-auth-divider` — `"New here?"` style rule with `<span>` label

### `Login.cshtml`
- Email + password + "Remember me".
- "Forgot password?" link next to remember-me checkbox.
- "Create account" button (gated by `Model.AllowPublicRegistration`).
- Renders `TempData["pb_toast"/"pb_toast_error"]` (used by reset flow).

### `Register.cshtml`
Simple email + password + confirm. Submits to create user and triggers the verify flow.

### `VerifyEmail.cshtml`
- Masked email banner + centered 5-digit input (`inputmode="numeric"`, `autocomplete="one-time-code"`).
- JS strips non-digits and auto-submits at 5 digits.
- Resend button with countdown hint (`ResendCooldownSeconds` initial).
- Displays `pb_toast` / `pb_toast_error` in coloured banner blocks.

### `ForgotPassword.cshtml`
Email input + "Send reset code" + "Back to sign in".

### `VerifyResetCode.cshtml`
Same 5-digit input pattern as VerifyEmail, plus resend form.

### `ResetPassword.cshtml`
New password + confirm + submit. Guarded server-side by `IsCodeVerified(session)`.

### `Logout.cshtml` / `AccessDenied.cshtml`
Minimal. Logout accepts POST only (CSRF safety).

---

## 4. Design system — `wwwroot/css/pixelbit.css`

The stylesheet is ~2,800 lines and implements the entire visual language. Key conceptual groups:

### 4.1 Theme primitives / variables
- `:root` — CSS custom properties for colors, durations, easings (e.g. `--pb-dur-3`, `--pb-ease`).
- Font stacks: `Inter`, `Space Grotesk`, `Orbitron`, `Cairo`.

### 4.2 Buttons & gradients
- **`.pb-btn-glow`** — a hoverable glow around Bootstrap `btn` variants.
- **`.pb-nav-cta-gradient`** — the pink→purple→cyan gradient CTA pill (used for "Book repair" in navbar).
- Primary button gradient is defined with 3 stops (cyan → purple → blue).

### 4.3 Surfaces
- **`.pb-surface`** + **`.pb-card`** — glassmorphism surface (`background rgba(255,255,255,.04)` + `backdrop-filter: blur(14-18px)` + inner border shadow).
- **`.pb-saas-card`**, **`.pb-service2`**, **`.pb-benefit`** — layered card families used in the homepage.
- **`.pb-action-card`** — uses `display:flex; flex-direction:column; padding:1.4rem 1.3rem;` so `.pb-action-btn` can `margin-top:auto`.
- **`.pb-action-btn`** — 46 px min-height, 14 px radius, `inline-flex center center`, `white-space:nowrap` — the rule that keeps the three "Get Started Instantly" buttons identical.

### 4.4 Hero
- **`.pb-hero2`** with layered backgrounds: `.pb-hero2-bg` (radial), `.pb-hero2-aurora`, `.pb-hero2-grid`, `.pb-hero2-sparks`.
- Parallax-friendly: elements with `data-parallax` are wiggled by `site.js`.

### 4.5 Navbar
- **`.pb-navbar-next`** has a soft glassy surface, turns `--scrolled` when the page scrolls past 8 px.
- **`.pb-nav-pill-wrap`** — the rounded nav-pill container on desktop.
- **`.pb-nav-icon-btn`** — 40×40 icon buttons (cart, hamburger) with focus rings.
- **`.pb-offcanvas`** — mobile drawer styling (dark blur, brand header, same gradient CTAs).

### 4.6 Auth shell (added in recent iterations)
```css
.pb-auth-shell  { max-width: 28rem; margin: 0 auto; padding-inline: .25rem; }
.pb-auth-card   { backdrop-filter: blur(15px) saturate(1.08); }
.pb-auth-btn    { width:100%; min-height:46px; border-radius:14px; font-weight:600; letter-spacing:.01em; }
.pb-auth-link   { text-decoration:none; color: rgba(235,235,235,.78); }
.pb-auth-divider{ text-align:center; letter-spacing:.18em; text-transform:uppercase; ... }
```

### 4.7 Responsiveness
- **Breakpoints** used with Bootstrap: `sm(576)`, `md(768)`, `lg(992)`, `xl(1200)`.
- Custom media queries for nav, hero, cards, section rhythm at `max-width: 991.98`, `767.98`, `575.98`, `430`, `360`.
- Mobile hardening block near the bottom of the file:
  ```css
  html, body { overflow-x: hidden; }
  @media (max-width: 575.98px) {
      .pb-nav-surface { gap:.5rem; flex-wrap:nowrap; }
      .pb-navbar-brand-next { min-width: 0; }
      .pb-nav-icon-btn { min-width: 40px; min-height: 40px; }
  }
  ```

### 4.8 RTL
- `.pb-rtl` overrides flip text-alignment where needed and select Cairo font for Arabic headings.
- `.pb-ltr-inline` forces phone numbers / ticket codes / times to render LTR inside RTL paragraphs.

### 4.9 Motion
- `.pb-fade` / `.pb-anim` get a `pb-in` class added by `site.js` when scrolled into view, driving a fade/rise/tilt/glow/pop/sheen entrance — controlled by:
  ```css
  @media (prefers-reduced-motion: reduce) {
      .pb-fade, .pb-anim, .pb-saas-card, .pb-service2, ... { transition: none !important; }
  }
  ```

### 4.10 Other component groups
- `.pb-work2` (Instagram gallery tiles with photo, chip, overlay)
- `.pb-visit` (full-bleed final CTA)
- `.pb-saas-cta` (generic section CTA band)
- `.pb-lang-toggle` / `.pb-lang-btn` (EN/AR pill)

---

## 5. `wwwroot/js/site.js`

A single, framework-free module (immediately invoked). Responsibilities:

1. **Fade-in on scroll** — `IntersectionObserver` adds `pb-in` to `.pb-fade` / `.pb-anim` elements; supports `data-pb-stagger="1..12"` for staggered reveals. Gracefully downgrades to unconditional reveal on older browsers.
2. **Scroll parallax variable** — sets `--pb-scroll` on `<html>` on `scroll` via `requestAnimationFrame`.
3. **Navbar scroll state** — toggles `.pb-navbar--scrolled` after 8 px.
4. **Device chips (booking form)** — listens on `.pb-chip` clicks, updates the hidden `DeviceType` input and toggles `is-active`.
5. **Cart AJAX** — all `form[data-cart-add]` or store card buttons call `fetch('/Cart/Add', { method:'POST', headers:{RequestVerificationToken:__pb.csrf}, body: JSON.stringify({productId, quantity}) })` and update `#pb-cart-count`.
6. **Keyboard safety** — closes offcanvas on Esc; focus-visible polyfills; prevents scroll jank on anchor links.

The individual auth pages include small inline scripts (see `VerifyEmail.cshtml` / `VerifyResetCode.cshtml`) for the 5-digit input auto-submit and the resend countdown.

---

## 6. Localization + RTL

### 6.1 Config (`Program.cs`)
```csharp
builder.Services.AddLocalization();
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(o =>
        o.DataAnnotationLocalizerProvider = (_, f) => f.Create(typeof(SharedResource)));

var supportedCultures = new[] { "en", "ar" }.Select(c => new CultureInfo(c)).ToList();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
    RequestCultureProviders = new[] { new CookieRequestCultureProvider() }
});
```
Only cookie-based culture resolution is used (so language selection persists, it's not locked to browser language).

### 6.2 Resource files
- `Resources/SharedResource.cs` — marker class for the resource lookup key.
- `Resources/SharedResource.resx` — English strings.
- `Resources/SharedResource.ar.resx` — Arabic strings.

### 6.3 Usage in views
```razor
@inject Microsoft.Extensions.Localization.IStringLocalizer<PixelAndBit.Web.SharedResource> T
<h1>@T["Home.Hero.Title"]</h1>
<span>@T["Store.ItemsCount", list.Count, suffix]</span>
```

### 6.4 ViewModels
ViewModels use the same keys via `Display(Name = "...")` + `ErrorMessage = "Validation.Required"`, and localization is wired through `DataAnnotationLocalizerProvider` → `SharedResource`.

### 6.5 Switching language
The navbar contains two `<form method="post" asp-controller="Language" asp-action="Set">` sub-buttons, passing `culture=en|ar` + current `returnUrl`. `LanguageController` writes the `.AspNetCore.Culture` cookie with 1-year expiry and `LocalRedirect`s back.

### 6.6 RTL presentation
- `_Layout.cshtml` writes `<html dir="rtl">` when culture is `ar` and loads `bootstrap.rtl.min.css` instead of the LTR bundle.
- `body` gets `pb-rtl` class, targeted by `pixelbit.css` rules to flip text-align, swap margins, and apply Cairo font to headings.
- `.pb-ltr-inline` is a utility we apply to phone numbers, ticket codes, time ranges, and WhatsApp numbers so digits never reverse.

---

## 7. CSRF + cookies in the UI

- Every form uses `@Html.AntiForgeryToken()` or the `asp-*` helpers that emit the hidden `__RequestVerificationToken` input.
- For JSON POSTs (cart add, booking admin status update, admin users API), the token is exposed via `window.__pb.csrf` in `_Layout.cshtml` and passed back as the `RequestVerificationToken` HTTP header.
- Cookies:
  - `.AspNetCore.Identity.Application` — auth cookie (Identity default).
  - `.PixelAndBit.Session` — session for cart + password-reset state.
  - `.AspNetCore.Culture` — language preference.
  - `.AspNetCore.Antiforgery.*` — CSRF token binding.

---

Continue to [`04_Database_Auth_Config.md`](./04_Database_Auth_Config.md) for the schema, authorization rules, and configuration files.
