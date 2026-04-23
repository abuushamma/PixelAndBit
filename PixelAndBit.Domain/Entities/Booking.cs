using PixelAndBit.Domain.Enums;

namespace PixelAndBit.Domain.Entities;

public class Booking
{
    public Guid Id { get; set; }

    public string TicketReference { get; set; } = string.Empty; // e.g. PB-2026-1A2B

    public string? UserId { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public string IssueDescription { get; set; } = string.Empty;

    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal EstimatedCost { get; set; }
}

