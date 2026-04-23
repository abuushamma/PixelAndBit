using Microsoft.EntityFrameworkCore;
using PixelAndBit.Application.Interfaces;
using PixelAndBit.Domain.Entities;
using PixelAndBit.Domain.Enums;

namespace PixelAndBit.Infrastructure.Data;

public class BookingService : IBookingService
{
    private readonly PixelBitDbContext _context;

    public BookingService(PixelBitDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string? TicketReference, string? ErrorMessage)> CreateBookingAsync(
        string? userId,
        string customerName,
        string phoneNumber,
        string deviceModel,
        string issueDescription,
        decimal estimatedCost)
    {
        customerName = (customerName ?? string.Empty).Trim();
        phoneNumber = (phoneNumber ?? string.Empty).Trim();
        deviceModel = (deviceModel ?? string.Empty).Trim();
        issueDescription = (issueDescription ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(customerName) || customerName.Length < 2)
            return (false, null, "Please enter your full name.");

        // Jordan (mobile): starts with 07 and 10 digits total e.g. 07XXXXXXXX
        if (!System.Text.RegularExpressions.Regex.IsMatch(phoneNumber, "^07\\d{8}$"))
            return (false, null, "Phone number must be in Jordanian format (07XXXXXXXX).");

        if (string.IsNullOrWhiteSpace(deviceModel) || deviceModel.Length < 2)
            return (false, null, "Please enter your device model.");

        if (string.IsNullOrWhiteSpace(issueDescription) || issueDescription.Length < 20)
            return (false, null, "Issue description must be detailed (at least 20 characters).");

        if (estimatedCost < 0)
            return (false, null, "Estimated cost cannot be negative.");

        var now = DateTime.UtcNow;
        var ticket = await GenerateTicketReferenceAsync(now.Year);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            TicketReference = ticket,
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim(),
            CustomerName = customerName,
            PhoneNumber = phoneNumber,
            DeviceModel = deviceModel,
            IssueDescription = issueDescription,
            EstimatedCost = estimatedCost,
            Status = BookingStatus.Pending,
            CreatedAt = now
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        return (true, ticket, null);
    }

    public async Task<IReadOnlyList<Booking>> GetAllAsync()
    {
        return await _context.Bookings
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<Booking?> GetByTicketAsync(string ticketReference)
    {
        ticketReference = (ticketReference ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(ticketReference))
            return null;

        return await _context.Bookings.FirstOrDefaultAsync(b => b.TicketReference == ticketReference);
    }

    public async Task<bool> UpdateStatusAsync(Guid bookingId, BookingStatus status)
    {
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null) return false;

        booking.Status = status;
        await _context.SaveChangesAsync();
        return true;
    }

    private async Task<string> GenerateTicketReferenceAsync(int year)
    {
        // PB-2026-XXXX (base-36 suffix). Retry on collision.
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var suffix = NextBase36(4);
            var candidate = $"PB-{year}-{suffix}";
            var exists = await _context.Bookings.AnyAsync(b => b.TicketReference == candidate);
            if (!exists) return candidate;
        }

        return $"PB-{year}-{Guid.NewGuid():N}".Substring(0, 13 + 8);
    }

    private static string NextBase36(int len)
    {
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var bytes = new byte[len];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var chars = new char[len];
        for (var i = 0; i < len; i++)
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }
}

