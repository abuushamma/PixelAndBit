using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PixelAndBit.Application.Interfaces;

namespace PixelAndBit.Web.Areas.Identity.Pages.Account;

public class VerifyResetCodeModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<VerifyResetCodeModel> _logger;

    public VerifyResetCodeModel(
        UserManager<IdentityUser> userManager,
        IEmailSender emailSender,
        ILogger<VerifyResetCodeModel> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Displayed to the user so they know which inbox to check.</summary>
    public string? MaskedEmail { get; private set; }

    public int ResendCooldownSeconds { get; private set; }

    public class InputModel
    {
        [Required]
        [RegularExpression("^\\d{5}$", ErrorMessage = "Enter the 5-digit code.")]
        [Display(Name = "Verification code")]
        public string Code { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (!PasswordResetFlow.HasPendingEmail(HttpContext.Session))
            return RedirectToPage("./ForgotPassword");

        PrepareViewState();
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!PasswordResetFlow.HasPendingEmail(HttpContext.Session))
            return RedirectToPage("./ForgotPassword");

        if (!ModelState.IsValid)
        {
            PrepareViewState();
            return Page();
        }

        var userId   = HttpContext.Session.GetString(PasswordResetFlow.K_UserId) ?? string.Empty;
        var email    = HttpContext.Session.GetString(PasswordResetFlow.K_Email) ?? string.Empty;
        var codeHash = HttpContext.Session.GetString(PasswordResetFlow.K_CodeHash) ?? string.Empty;
        var expRaw   = HttpContext.Session.GetString(PasswordResetFlow.K_ExpiresTicks) ?? "0";
        var attRaw   = HttpContext.Session.GetString(PasswordResetFlow.K_Attempts) ?? "0";
        var maxRaw   = HttpContext.Session.GetString(PasswordResetFlow.K_MaxAttempts)
                        ?? PasswordResetFlow.DefaultMaxAttempts.ToString();

        _ = long.TryParse(expRaw, out var expTicks);
        _ = int.TryParse(attRaw, out var attempts);
        _ = int.TryParse(maxRaw, out var maxAttempts);

        // Bound brute-force attempts regardless of whether a real code exists for the email.
        if (attempts >= maxAttempts)
        {
            PasswordResetFlow.ClearAll(HttpContext.Session);
            return RedirectToExpired();
        }

        // Count this attempt BEFORE comparing so retries are bounded even on crash.
        attempts += 1;
        HttpContext.Session.SetString(PasswordResetFlow.K_Attempts, attempts.ToString());

        // No real code was ever issued for this email (non-existent or unconfirmed account).
        // Respond with the same generic error — never leak whether the email is registered.
        if (string.IsNullOrEmpty(codeHash) || string.IsNullOrEmpty(userId))
        {
            ModelState.AddModelError(string.Empty, "Wrong code. Please try again.");
            PrepareViewState();
            return Page();
        }

        // Real code was issued — enforce expiry.
        if (expTicks <= 0 || DateTime.UtcNow.Ticks > expTicks)
        {
            PasswordResetFlow.ClearAll(HttpContext.Session);
            return RedirectToExpired();
        }

        var submitted = PasswordResetFlow.HashCode(userId, email, Input.Code);
        if (!PasswordResetFlow.FixedTimeEquals(submitted, codeHash))
        {
            ModelState.AddModelError(string.Empty, "Wrong code. Please try again.");
            PrepareViewState();
            return Page();
        }

        HttpContext.Session.SetString(PasswordResetFlow.K_Verified, "1");
        // Clear the code hash so the same code can't be replayed by re-submitting this step.
        HttpContext.Session.Remove(PasswordResetFlow.K_CodeHash);

        return RedirectToPage("./ResetPassword");
    }

    public async Task<IActionResult> OnPostResendAsync()
    {
        if (!PasswordResetFlow.HasPendingEmail(HttpContext.Session))
            return RedirectToPage("./ForgotPassword");

        var cooldown = PasswordResetFlow.ResendCooldownSeconds_(HttpContext.Session);
        if (cooldown > 0)
        {
            TempData["pb_toast_error"] = $"Please wait {cooldown} second(s) before requesting a new code.";
            return RedirectToPage();
        }

        var email  = HttpContext.Session.GetString(PasswordResetFlow.K_Email)  ?? string.Empty;
        var userId = HttpContext.Session.GetString(PasswordResetFlow.K_UserId) ?? string.Empty;
        var now = DateTime.UtcNow;

        // Always advance the cooldown so a resend-spam attacker can't distinguish
        // registered-vs-unregistered emails by response timing either.
        HttpContext.Session.SetString(PasswordResetFlow.K_LastSentTicks, now.Ticks.ToString());
        HttpContext.Session.SetString(PasswordResetFlow.K_Attempts, "0");

        // Look up by id first (fast path when Step 1 found a real user);
        // otherwise fall back to email lookup.
        IdentityUser? user = !string.IsNullOrEmpty(userId)
            ? await _userManager.FindByIdAsync(userId)
            : null;
        user ??= !string.IsNullOrWhiteSpace(email)
            ? await _userManager.FindByEmailAsync(email)
            : null;

        if (user != null && await _userManager.IsEmailConfirmedAsync(user))
        {
            var code = PasswordResetFlow.Generate5DigitCode();
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var expires = now.AddMinutes(PasswordResetFlow.CodeTtlMinutes);

            HttpContext.Session.SetString(PasswordResetFlow.K_UserId, user.Id);
            HttpContext.Session.SetString(PasswordResetFlow.K_CodeHash,
                PasswordResetFlow.HashCode(user.Id, email, code));
            HttpContext.Session.SetString(PasswordResetFlow.K_ExpiresTicks, expires.Ticks.ToString());
            HttpContext.Session.SetString(PasswordResetFlow.K_ResetToken, resetToken);
            HttpContext.Session.Remove(PasswordResetFlow.K_Verified);

            try
            {
                await _emailSender.SendAsync(
                    email,
                    "Reset your password – Pixel & Bit",
                    PasswordResetFlow.BuildResetEmailHtml(code));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resend password reset email.");
            }
        }

        // Always report the same neutral success message.
        TempData["pb_toast"] = "If an account exists for that email, a new code has been sent.";
        return RedirectToPage();
    }

    private IActionResult RedirectToExpired()
    {
        TempData["pb_toast_error"] = "Your reset code is no longer valid. Please request a new one.";
        return RedirectToPage("./ForgotPassword");
    }

    private void PrepareViewState()
    {
        var email = HttpContext.Session.GetString(PasswordResetFlow.K_Email);
        MaskedEmail = MaskEmail(email);
        ResendCooldownSeconds = PasswordResetFlow.ResendCooldownSeconds_(HttpContext.Session);
    }

    private static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.IndexOf('@');
        if (at < 1) return email;

        var local = email[..at];
        var domain = email[at..];
        if (local.Length <= 2) return local[..1] + "***" + domain;
        return local[..1] + new string('*', Math.Min(6, local.Length - 2)) + local[^1] + domain;
    }
}
