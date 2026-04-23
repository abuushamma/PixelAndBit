namespace PixelAndBit.Domain.Entities;

public class EmailVerificationCode
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }

    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 6;

    public DateTime? ConsumedAtUtc { get; set; }
}

