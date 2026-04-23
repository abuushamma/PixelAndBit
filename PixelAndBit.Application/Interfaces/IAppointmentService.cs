using PixelAndBit.Domain.Entities;

namespace PixelAndBit.Application.Interfaces;

public interface IAppointmentService
{
    Task<IEnumerable<RepairService>> GetAllRepairServicesAsync();
    Task<IEnumerable<DateTime>> GetAvailableDatesAsync(int serviceId, int month, int year);
    Task<IEnumerable<TimeSpan>> GetAvailableSlotsAsync(int serviceId, DateTime date);
    Task<bool> CreateAppointmentAsync(Appointment appointment);
}

