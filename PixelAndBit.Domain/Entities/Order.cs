using PixelAndBit.Domain.Enums;

namespace PixelAndBit.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }

    public string? UserId { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

