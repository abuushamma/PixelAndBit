using PixelAndBit.Domain.Enums;

namespace PixelAndBit.Domain.Entities;

public class Appointment
{
    public int Id { get; set; }
    public string ConfirmationCode { get; set; } = string.Empty; // e.g. "PB-4821"

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string DeviceDescription { get; set; } = string.Empty;

    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public string? Notes { get; set; }              // Internal staff notes
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int RepairServiceId { get; set; }
    public RepairService RepairService { get; set; } = null!;
}