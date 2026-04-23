using Microsoft.EntityFrameworkCore;
using PixelAndBit.Application.Interfaces;
using PixelAndBit.Domain.Entities;

namespace PixelAndBit.Infrastructure.Data;

public class AppointmentService : IAppointmentService
{
    private readonly PixelBitDbContext _context;

    public AppointmentService(PixelBitDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<RepairService>> GetAllRepairServicesAsync()
    {
        return await _context.RepairServices.ToListAsync();
    }

    public async Task<bool> CreateAppointmentAsync(Appointment appointment)
    {
        appointment.ConfirmationCode = "PB-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
        _context.Appointments.Add(appointment);
        return await _context.SaveChangesAsync() > 0;
    }

    public Task<IEnumerable<DateTime>> GetAvailableDatesAsync(int serviceId, int month, int year)
        => Task.FromResult(Enumerable.Empty<DateTime>());

    public Task<IEnumerable<TimeSpan>> GetAvailableSlotsAsync(int serviceId, DateTime date)
        => Task.FromResult(Enumerable.Empty<TimeSpan>());
}

