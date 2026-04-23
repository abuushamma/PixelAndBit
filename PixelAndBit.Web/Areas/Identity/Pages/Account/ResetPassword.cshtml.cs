using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PixelAndBit.Web.Areas.Identity.Pages.Account;

public class ResetPasswordModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;

    public ResetPasswordModel(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (!PasswordResetFlow.IsCodeVerified(HttpContext.Session))
        {
            TempData["pb_toast_error"] = "Please verify your code before setting a new password.";
            return RedirectToPage("./ForgotPassword");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!PasswordResetFlow.IsCodeVerified(HttpContext.Session))
        {
            TempData["pb_toast_error"] = "Please verify your code before setting a new password.";
            return RedirectToPage("./ForgotPassword");
        }

        if (!ModelState.IsValid)
            return Page();

        var userId = HttpContext.Session.GetString(PasswordResetFlow.K_UserId) ?? string.Empty;
        var resetToken = HttpContext.Session.GetString(PasswordResetFlow.K_ResetToken) ?? string.Empty;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || string.IsNullOrWhiteSpace(resetToken))
        {
            PasswordResetFlow.ClearAll(HttpContext.Session);
            TempData["pb_toast_error"] = "Your reset session has expired. Please start again.";
            return RedirectToPage("./ForgotPassword");
        }

        var result = await _userManager.ResetPasswordAsync(user, resetToken, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError(string.Empty, err.Description);
            return Page();
        }

        // Invalidate any existing sign-in sessions so this user must re-login with the new password.
        await _userManager.UpdateSecurityStampAsync(user);

        // Wipe the reset state — flow is one-time-use end-to-end.
        PasswordResetFlow.ClearAll(HttpContext.Session);

        TempData["pb_toast"] = "Your password has been reset. Please sign in with your new password.";
        return RedirectToPage("./Login");
    }
}
