using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PixelAndBit.Web.Areas.Identity.Pages.Account;

public class AccessDeniedModel : PageModel
{
    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
    }
}
