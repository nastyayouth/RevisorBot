namespace WebApplication1.Entities;

public enum ProductStatus
{
    Pending = 0,
    Confirmed = 1
}

public class Product
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string ProductName { get; set; } = "";
    public DateTime? ExpiryDate { get; set; }

    public string? TelegramPhotoFileId { get; set; }
    public string? Notes { get; set; }
    public double Confidence { get; set; }

    public string? NotifiedForMonth { get; set; } // например "2026-01"
    public DateTime CreatedAtUtc { get; set; }
    
    public ProductStatus Status { get; set; } = ProductStatus.Pending;
}
