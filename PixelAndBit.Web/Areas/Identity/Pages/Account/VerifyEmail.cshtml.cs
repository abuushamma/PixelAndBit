using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PixelAndBit.Application.Interfaces;
using PixelAndBit.Domain.Entities;
using PixelAndBit.Infrastructure.Data;

namespace PixelAndBit.Web.Areas.Identity.Pages.Account;

public class VerifyEmailModel : PageModel
{
    private const int ResendCooldownPeriodSeconds = 60;
    private const int MaxResendsPerHour = 10;

    private readonly PixelBitDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IEmailSender _emailSender;

    public VerifyEmailModel(
        PixelBitDbContext db,
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        IEmailSender emailSender)
    {
        _db = db;
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
    }

    [BindProperty(SupportsGet = true)]
    public string Email { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Seconds until the user may request another resend (UI cooldown).</summary>
    public int ResendCooldownSeconds { get; set; }

    public class InputModel
    {
        [Required]
        [RegularExpression("^\\d{5}$", ErrorMessage = "Enter the 5-digit code.")]
        public string Code { get; set; } = "";
    }

    public async Task<IActionResult> OnGet(string? email = null, string? returnUrl = null)
    {
        Email = email ?? Email;
        ReturnUrl = returnUrl ?? ReturnUrl ?? Url.Content("~/");
        if (string.IsNullOrWhiteSpace(Email))
            return RedirectToPage("./Register");

        var user = await _userManager.FindByEmailAsync(Email);
        if (user != null && !await _userManager.IsEmailConfirmedAsync(user))
            ResendCooldownSeconds = await GetResendCooldownSecondsAsync(user.Id);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl ??= Url.Content("~/");
        if (!ModelState.IsValid)
        {
            await ApplyCooldownForCurrentEmailAsync();
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Account not found. Please register again.");
            return Page();
        }

        if (await _userManager.IsEmailConfirmedAsync(user))
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(ReturnUrl);
        }

        var now = DateTime.UtcNow;
        var rec = await _db.EmailVerificationCodes
            .Where(x => x.UserId == user.Id && x.Email == Email && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (rec == null)
        {
            ModelState.AddModelError(string.Empty, "Code expired. Please resend a new code.");
            await ApplyCooldownForUserAsync(user);
            return Page();
        }

        if (rec.Attempts >= rec.MaxAttempts)
        {
            ModelState.AddModelError(string.Empty, "Too many attempts. Please resend a new code.");
            await ApplyCooldownForUserAsync(user);
            return Page();
        }

        rec.Attempts += 1;
        var ok = FixedTimeEquals(HashCode(user.Id, Email, Input.Code), rec.CodeHash);
        if (!ok)
        {
            await _db.SaveChangesAsync();
            ModelState.AddModelError(string.Empty, "Wrong code. Try again.");
            await ApplyCooldownForUserAsync(user);
            return Page();
        }

        rec.ConsumedAtUtc = now;
        await _db.SaveChangesAsync();

        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);

        await _signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(ReturnUrl);
    }

    public async Task<IActionResult> OnPostResendAsync(string email, string? returnUrl = null)
    {
        Email = email;
        ReturnUrl = returnUrl ?? Url.Content("~/");

        var user = await _userManager.FindByEmailAsync(Email);
        if (user == null)
            return RedirectToPage("./Register");

        if (await _userManager.IsEmailConfirmedAsync(user))
            return RedirectToPage("./Login", new { returnUrl = ReturnUrl });

        var now = DateTime.UtcNow;

        var latest = await _db.EmailVerificationCodes
            .Where(x => x.UserId == user.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (latest != default)
        {
            var since = (now - latest).TotalSeconds;
            if (since < ResendCooldownPeriodSeconds)
            {
                var wait = (int)Math.Ceiling(ResendCooldownPeriodSeconds - since);
                TempData["pb_toast_error"] = $"Please wait {wait} second(s) before requesting a new code.";
                return RedirectToPage("./VerifyEmail", new { email = Email, returnUrl = ReturnUrl });
            }
        }

        var hourAgo = now.AddHours(-1);
        var resendsLastHour = await _db.EmailVerificationCodes
            .CountAsync(x => x.UserId == user.Id && x.CreatedAtUtc >= hourAgo);

        if (resendsLastHour >= MaxResendsPerHour)
        {
            TempData["pb_toast_error"] = "Too many verification emails sent. Please try again in about an hour.";
            return RedirectToPage("./VerifyEmail", new { email = Email, returnUrl = ReturnUrl });
        }

        var code = Generate5DigitCode();

        _db.EmailVerificationCodes.Add(new EmailVerificationCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Email = Email,
            CodeHash = HashCode(user.Id, Email, code),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(10),
            Attempts = 0,
            MaxAttempts = 6
        });
        await _db.SaveChangesAsync();

        try
        {
            await _emailSender.SendAsync(
                Email,
                "Verify your email – Pixel & Bit",
                BuildEmailHtml(code));
        }
        catch (Exception ex)
        {
            TempData["pb_toast_error"] = $"We couldn't send the verification code email right now. Please try again later. ({ex.Message})";
        }

        TempData["pb_toast"] = "New code sent. Check your email.";
        return RedirectToPage("./VerifyEmail", new { email = Email, returnUrl = ReturnUrl });
    }

    private async Task ApplyCooldownForCurrentEmailAsync()
    {
        var user = await _userManager.FindByEmailAsync(Email);
        if (user != null)
            ResendCooldownSeconds = await GetResendCooldownSecondsAsync(user.Id);
    }

    private async Task ApplyCooldownForUserAsync(IdentityUser user)
    {
        ResendCooldownSeconds = await GetResendCooldownSecondsAsync(user.Id);
    }

    private async Task<int> GetResendCooldownSecondsAsync(string userId)
    {
        var last = await _db.EmailVerificationCodes
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => (DateTime?)x.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (last == null)
            return 0;

        var elapsed = (DateTime.UtcNow - last.Value).TotalSeconds;
        if (elapsed >= ResendCooldownPeriodSeconds)
            return 0;

        return (int)Math.Ceiling(ResendCooldownPeriodSeconds - elapsed);
    }

    internal static string Generate5DigitCode()
    {
        var n = RandomNumberGenerator.GetInt32(10000, 100000);
        return n.ToString();
    }

    internal static string HashCode(string userId, string email, string code)
    {
        var input = $"{userId}|{email.ToUpperInvariant()}|{code}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    internal static bool FixedTimeEquals(string a, string b)
    {
        var ab = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ab.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ab, bb);
    }

    /// <summary>HTML email for verification (registration and resend). Code is numeric only.</summary>
    internal static string BuildEmailHtml(string code, bool includeRegistrationWelcome = false)
    {
        var welcome = includeRegistrationWelcome
            ? "<p style=\"margin:0 0 16px;font-family:'Segoe UI',Arial,sans-serif;font-size:15px;line-height:1.6;color:#1f2328;\">Thanks for registering with <strong style=\"color:#111418;\">Pixel &amp; Bit</strong>.</p>"
            : "";

        // Plain-text preview shown by some clients in the inbox list.
        var preheader = $"Your Pixel & Bit verification code is {code}. This code expires in 10 minutes.";

        return $@"
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"" lang=""en"">
<head>
  <meta charset=""utf-8""/>
  <meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
  <meta name=""x-apple-disable-message-reformatting""/>
  <meta name=""color-scheme"" content=""light only""/>
  <meta name=""supported-color-schemes"" content=""light""/>
  <title>Verify your email</title>
  <style>
    @@media only screen and (max-width: 600px) {{
      .pb-container {{ width: 100% !important; max-width: 100% !important; }}
      .pb-pad-x    {{ padding-left: 20px !important; padding-right: 20px !important; }}
      .pb-pad-y    {{ padding-top: 26px !important; padding-bottom: 26px !important; }}
      .pb-code     {{ font-size: 30px !important; letter-spacing: 0.28em !important; padding: 16px 20px !important; }}
      .pb-title    {{ font-size: 20px !important; }}
      .pb-body     {{ font-size: 15px !important; }}
    }}
  </style>
</head>
<body style=""margin:0;padding:0;background-color:#f4f5f7;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%;"">
  <!-- Preheader (hidden preview text) -->
  <div style=""display:none;max-height:0;overflow:hidden;mso-hide:all;font-size:1px;line-height:1px;color:#f4f5f7;opacity:0;"">{preheader}</div>

  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#f4f5f7;"">
    <tr>
      <td align=""center"" style=""padding:28px 16px 40px;"">

        <!-- Brand line -->
        <table role=""presentation"" width=""560"" class=""pb-container"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""width:560px;max-width:560px;margin:0 auto 14px;"">
          <tr>
            <td align=""center"" style=""font-family:'Segoe UI',Arial,sans-serif;font-size:14px;font-weight:700;letter-spacing:0.06em;color:#111418;"">
              Pixel &amp; Bit
            </td>
          </tr>
        </table>

        <!-- Main white card -->
        <table role=""presentation"" width=""560"" class=""pb-container"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""width:560px;max-width:560px;background-color:#ffffff;border:1px solid #e5e7eb;border-radius:8px;"">
          <!-- Header / title -->
          <tr>
            <td class=""pb-pad-x"" style=""padding:28px 40px 0;text-align:left;"">
              <h1 class=""pb-title"" style=""margin:0 0 6px;font-family:'Segoe UI',Arial,sans-serif;font-size:22px;font-weight:600;color:#111418;line-height:1.3;"">Verify your email</h1>
              <p style=""margin:0;font-family:'Segoe UI',Arial,sans-serif;font-size:13px;color:#6b7280;line-height:1.5;"">Confirm your email address to activate your Pixel &amp; Bit account.</p>
            </td>
          </tr>

          <!-- Divider -->
          <tr>
            <td style=""padding:20px 40px 0;"">
              <div style=""height:1px;line-height:1px;font-size:0;background-color:#e5e7eb;"">&nbsp;</div>
            </td>
          </tr>

          <!-- Body -->
          <tr>
            <td class=""pb-pad-x pb-pad-y"" style=""padding:24px 40px 8px;"">
              {welcome}
              <p class=""pb-body"" style=""margin:0 0 20px;font-family:'Segoe UI',Arial,sans-serif;font-size:15px;line-height:1.6;color:#1f2328;"">
                Enter the verification code below to activate your account.
              </p>

              <!-- Code box -->
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" align=""center"" style=""margin:4px auto 8px;"">
                <tr>
                  <td class=""pb-code"" align=""center"" style=""padding:18px 28px;background-color:#f9fafb;border:1px solid #d1d5db;border-radius:6px;font-family:Consolas,'Courier New',monospace;font-size:34px;font-weight:700;letter-spacing:0.32em;color:#111418;line-height:1;"">
                    {code}
                  </td>
                </tr>
              </table>

              <p style=""margin:18px 0 0;font-family:'Segoe UI',Arial,sans-serif;font-size:14px;line-height:1.6;color:#4b5563;"">
                This code expires in <strong style=""color:#111418;"">10 minutes</strong>.
              </p>
              <p style=""margin:6px 0 0;font-family:'Segoe UI',Arial,sans-serif;font-size:13px;line-height:1.6;color:#6b7280;"">
                If you did not request this email, you can safely ignore it. Your account will remain inactive.
              </p>
            </td>
          </tr>

          <!-- Closing -->
          <tr>
            <td class=""pb-pad-x"" style=""padding:0 40px 28px;"">
              <p style=""margin:14px 0 0;font-family:'Segoe UI',Arial,sans-serif;font-size:14px;color:#1f2328;line-height:1.6;"">
                Thank you,<br/>
                <span style=""color:#111418;font-weight:600;"">The Pixel &amp; Bit Team</span>
              </p>
            </td>
          </tr>
        </table>

        <!-- Footer -->
        <table role=""presentation"" width=""560"" class=""pb-container"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""width:560px;max-width:560px;margin:18px auto 0;"">
          <tr>
            <td align=""center"" style=""font-family:'Segoe UI',Arial,sans-serif;font-size:12px;color:#6b7280;line-height:1.6;"">
              This is an automated message. Please do not reply.<br/>
              Pixel &amp; Bit &middot; Amman, Jordan
            </td>
          </tr>
        </table>

      </td>
    </tr>
  </table>
</body>
</html>";
    }
}
