using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PixelAndBit.Application.Interfaces;

namespace PixelAndBit.Web.Areas.Identity.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IConfiguration _configuration;

    public LoginModel(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IConfiguration configuration)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _configuration = configuration;
    }

    /// <summary>When false, public self-registration is disabled (admin-only users).</summary>
    public bool AllowPublicRegistration { get; set; }

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
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    public Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        AllowPublicRegistration = _configuration.GetValue("AllowPublicRegistration", false);
        // Do not SignOut here — it would log out the admin on every visit to the login page.
        return Task.CompletedTask;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        AllowPublicRegistration = _configuration.GetValue("AllowPublicRegistration", false);
        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user != null && !await _userManager.IsEmailConfirmedAsync(user))
        {
            TempData["pb_toast"] = "Your email is not verified yet. Enter the 5-digit code we sent you (or resend).";
            return RedirectToPage("./VerifyEmail", new { email = Input.Email, returnUrl = ReturnUrl });
        }

        var result = await _signInManager.PasswordSignInAsync(
            Input.Email,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
            return LocalRedirect(ReturnUrl);

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(
                string.Empty,
                "This account is locked after too many failed sign-in attempts. Please try again in 15 minutes.");
            return Page();
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Sign in is not allowed for this account.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return Page();
    }
}

