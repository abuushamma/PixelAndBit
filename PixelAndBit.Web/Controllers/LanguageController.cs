using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace PixelAndBit.Web.Controllers;

public sealed class LanguageController : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Set(string culture, string returnUrl)
    {
        culture = (culture ?? "en").ToLowerInvariant();
        if (culture is not ("en" or "ar")) culture = "en";

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                IsEssential = true,
                HttpOnly = false,
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            }
        );

        if (!Url.IsLocalUrl(returnUrl)) returnUrl = Url.Content("~/");
        return LocalRedirect(returnUrl);
    }
}

