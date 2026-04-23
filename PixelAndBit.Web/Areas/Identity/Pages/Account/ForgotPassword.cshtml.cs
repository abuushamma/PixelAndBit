using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PixelAndBit.Application.Interfaces;

namespace PixelAndBit.Web.Areas.Identity.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(
        UserManager<IdentityUser> userManager,
        IEmailSender emailSender,
        ILogger<ForgotPasswordModel> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var email = (Input.Email ?? string.Empty).Trim();

        PasswordResetFlow.ClearAll(HttpContext.Session);

        var now = DateTime.UtcNow;

        // ALWAYS persist the email + cooldown + attempt counter in session so Step 2
        // is reachable regardless of whether a real account was found. This keeps the
        // response neutral (no info leak) and matches the required multi-step UX.
        HttpContext.Session.SetString(PasswordResetFlow.K_Email, email);
        HttpContext.Session.SetString(PasswordResetFlow.K_Attempts, "0");
        HttpContext.Session.SetString(PasswordResetFlow.K_MaxAttempts,
            PasswordResetFlow.DefaultMaxAttempts.ToString());
        HttpContext.Session.SetString(PasswordResetFlow.K_LastSentTicks, now.Ticks.ToString());

        var user = await _userManager.FindByEmailAsync(email);
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

            try
            {
                await _emailSender.SendAsync(
                    email,
                    "Reset your password – Pixel & Bit",
                    PasswordResetFlow.BuildResetEmailHtml(code));
            }
            catch (Exception ex)
            {
                // Never leak delivery errors to the caller — they'd reveal the account exists.
                _logger.LogWarning(ex, "Failed to send password reset email for an existing user.");
            }
        }
        else
        {
            // Simulate roughly the same work to make timing less telling. No code is
            // stored in session, so any code the user enters at Step 2 will fail the
            // hash check with the same "Wrong code" message.
            await Task.Delay(Random.Shared.Next(40, 120));
        }

        // Always respond the same way and always move the user forward to Step 2.
        TempData["pb_toast"] = "If an account exists for that email, a reset code has been sent.";
        return RedirectToPage("./VerifyResetCode");
    }
}
