using PixelAndBit.Domain.Entities;
using PixelAndBit.Domain.Enums;

namespace PixelAndBit.Application.Interfaces;

public interface IBookingService
{
    Task<(bool Success, string? TicketReference, string? ErrorMessage)> CreateBookingAsync(
        string? userId,
        string customerName,
        string phoneNumber,
        string deviceModel,
        string issueDescription,
        decimal estimatedCost);

    Task<IReadOnlyList<Booking>> GetAllAsync();

    Task<Booking?> GetByTicketAsync(string ticketReference);

    Task<bool> UpdateStatusAsync(Guid bookingId, BookingStatus status);
}