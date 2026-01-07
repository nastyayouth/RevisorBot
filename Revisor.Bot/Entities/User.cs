namespace WebApplication1.Entities;

public class User
{
    public Guid Id { get; set; }
    public long TelegramChatId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public List<Product> Products { get; set; } = new();
}
