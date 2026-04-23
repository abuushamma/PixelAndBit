using Microsoft.AspNetCore.Mvc;
using PixelAndBit.Application.Interfaces;

namespace PixelAndBit.Web.Controllers;

public class StoreController : Controller
{
    private readonly IProductService _productService;

    public StoreController(IProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _productService.GetAllProductsAsync();

        var q = (Request.Query["q"].ToString() ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.ToUpperInvariant();
            products = products.Where(p =>
                (p.Name ?? string.Empty).ToUpperInvariant().Contains(needle) ||
                (p.Description ?? string.Empty).ToUpperInvariant().Contains(needle));
        }

        ViewBag.Q = q;
        return View(products.ToList());
    }
}