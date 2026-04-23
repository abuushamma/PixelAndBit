namespace PixelAndBit.Web.Models;

public class AdminDashboardVm
{
    public int TotalProducts { get; set; }
    public int PendingRepairs { get; set; }
    public int TotalRequests { get; set; }
    public int TotalUsers { get; set; }

    public decimal ServicesRevenueJod { get; set; }
    public decimal SalesRevenueJod { get; set; }

    public IReadOnlyList<(string DeviceModel, int Count)> TopDeviceModels { get; set; } = Array.Empty<(string, int)>();
}

