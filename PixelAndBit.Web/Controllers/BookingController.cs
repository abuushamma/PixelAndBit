using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelAndBit.Application.Interfaces;
using PixelAndBit.Domain.Enums;
using PixelAndBit.Web.Models;

namespace PixelAndBit.Web.Controllers;

public class BookingController : Controller
{
    private readonly IBookingService _bookingService;

    public BookingController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new CreateBookingVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(CreateBookingVm vm)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", vm);
        }

        var deviceModel = $"{vm.DeviceType}: {vm.DeviceModel}".Trim();

        var result = await _bookingService.CreateBookingAsync(
            userId: User.Identity?.IsAuthenticated == true ? User.Identity?.Name : null,
            customerName: vm.CustomerName,
            phoneNumber: vm.PhoneNumber,
            deviceModel: deviceModel,
            issueDescription: vm.IssueDescription,
            estimatedCost: vm.EstimatedCost ?? 0m);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to create booking.");
            return View("Index", vm);
        }

        return RedirectToAction("Success", new { ticket = result.TicketReference });
    }

    [HttpGet]
    public IActionResult Success(string ticket)
    {
        ViewBag.Ticket = ticket;
        return View();
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Admin()
    {
        var bookings = await _bookingService.GetAllAsync();
        var q = (Request.Query["q"].ToString() ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.ToUpperInvariant();
            bookings = bookings
                .Where(b =>
                    b.TicketReference.ToUpperInvariant().Contains(needle) ||
                    b.CustomerName.ToUpperInvariant().Contains(needle) ||
                    b.PhoneNumber.ToUpperInvariant().Contains(needle))
                .ToList();
        }

        ViewBag.Q = q;
        return View(bookings);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateBookingStatusRequest req)
    {
        var ok = await _bookingService.UpdateStatusAsync(req.BookingId, req.Status);
        if (!ok) return NotFound();
        return Ok(new { status = req.Status.ToString() });
    }

    [HttpGet]
    public IActionResult Track()
    {
        return View(new TrackBookingVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Track(TrackBookingVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var booking = await _bookingService.GetByTicketAsync(vm.TicketReference);
        vm.Result = booking;

        if (booking == null)
            ModelState.AddModelError(nameof(vm.TicketReference), "Ticket not found. Please double-check the reference.");

        return View(vm);
    }
}

public record UpdateBookingStatusRequest(Guid BookingId, BookingStatus Status);