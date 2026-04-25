using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using PixelAndBit.Application.Interfaces;
using PixelAndBit.Domain.Entities;
using PixelAndBit.Infrastructure.Data;

namespace PixelAndBit.Web.Areas.Identity.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly PixelBitDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;

    public RegisterModel(
        UserManager<IdentityUser> userManager,
        PixelBitDbContext db,
        IEmailSender emailSender,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _db = db;
        _emailSender = emailSender;
        _configuration = configuration;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (!_configuration.GetValue("AllowPublicRegistration", false))
        {
            TempData["pb_toast_error"] = "Public registration is disabled. Please contact an administrator.";
            return RedirectToPage("./Login", new { returnUrl });
        }

        ReturnUrl = returnUrl ?? Url.Content("~/");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        if (!_configuration.GetValue("AllowPublicRegistration", false))
        {
            TempData["pb_toast_error"] = "Public registration is disabled.";
            return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
        }

        if (!ModelState.IsValid)
            return Page();

        var user = new IdentityUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            var code = VerifyEmailModel.Generate5DigitCode();
            var now = DateTime.UtcNow;

            _db.EmailVerificationCodes.Add(new EmailVerificationCode
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Email = user.Email ?? Input.Email,
                CodeHash = VerifyEmailModel.HashCode(user.Id, user.Email ?? Input.Email, code),
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(10),
                Attempts = 0,
                MaxAttempts = 6
            });
            await _db.SaveChangesAsync();

            try
            {
                await _emailSender.SendAsync(
                    Input.Email,
                    "Verify your email – Pixel & Bit",
                    VerifyEmailModel.BuildEmailHtml(code, includeRegistrationWelcome: true));
            }
            catch (Exception ex)
            {
                // Don't crash the whole request if SMTP isn't configured yet.
                // Never show the code on-screen; require SMTP for real delivery.
                TempData["pb_toast_error"] = $"We couldn't send the verification code email right now. Please try again later. ({ex.Message})";
            }

            return RedirectToPage("./VerifyEmail", new { email = Input.Email, returnUrl = ReturnUrl });
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return Page();
    }
}

