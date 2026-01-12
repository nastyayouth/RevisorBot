using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using WebApplication1.Entities;


public class ProductService : IProductService
{
    private readonly AppDbContext _db;
    private readonly TelegramFileService _files;
    private readonly IOpenAiProductExtractor _extractor;
    private readonly ITelegramSender _sender;

    public ProductService(
        AppDbContext db,
        TelegramFileService files,
        IOpenAiProductExtractor extractor,
        ITelegramSender sender)
    {
        _db = db;
        _files = files;
        _extractor = extractor;
        _sender = sender;
    }

    public async Task HandleMessageAsync(Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var user = await UpsertUser(chatId, ct);

        if (message.Text == "/start")
        {
            await _sender.SendTextAsync(chatId,
                "Hi! Send a product photo. I will extract name and expiry date and ask for confirmation.\n\n/list\n/delete <id>",
                ct);
            return;
        }

        if (message.Text == "/list")
        {
            var items = await _db.Products
                .Where(p => p.UserId == user.Id && p.Status == ProductStatus.Confirmed)
                .OrderByDescending(p => p.CreatedAtUtc)
                .Take(50)
                .ToListAsync(ct);

            if (items.Count == 0)
            {
                await _sender.SendTextAsync(chatId, "Your list is empty.", ct);
                return;
            }

            var lines = items.Select(p =>
                $"{p.Id} | {p.ProductName} | {(p.ExpiryDate?.ToString("yyyy-MM-dd") ?? "—")}");

            await _sender.SendTextAsync(chatId, string.Join("\n", lines), ct);
            return;
        }

        if (message.Text != null && message.Text.StartsWith("/delete", StringComparison.OrdinalIgnoreCase))
        {
            await DeleteByCommand(user.Id, chatId, message.Text, ct);
            return;
        }

        if (message.Photo is { Length: > 0 })
        {
            await HandlePhotoAsync(user.Id, chatId, message, ct);
            return;
        }

        await _sender.SendTextAsync(chatId, "Send a photo or use /list, /delete <id>.", ct);
    }

    public async Task HandleCallbackAsync(CallbackQuery cb, CancellationToken ct)
    {
        var chatId = cb.Message!.Chat.Id;
        var user = await UpsertUser(chatId, ct);

        var data = cb.Data ?? "";
        if (data.StartsWith("confirm:") && Guid.TryParse(data["confirm:".Length..], out var confirmId))
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == confirmId && p.UserId == user.Id, ct);
            if (product == null)
            {
                await _sender.AnswerCallbackAsync(cb.Id, "Not found", ct);
                return;
            }

            product.Status = ProductStatus.Confirmed;
            await _db.SaveChangesAsync(ct);

            await _sender.AnswerCallbackAsync(cb.Id, "Saved", ct);
            await _sender.EditTextAsync(chatId, cb.Message.MessageId,
                $"Saved.\n{product.ProductName}\nExpiry: {product.ExpiryDate:yyyy-MM-dd}", ct);
            return;
        }

        if (data.StartsWith("discard:") && Guid.TryParse(data["discard:".Length..], out var discardId))
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == discardId && p.UserId == user.Id, ct);
            if (product != null)
            {
                _db.Products.Remove(product);
                await _db.SaveChangesAsync(ct);
            }

            await _sender.AnswerCallbackAsync(cb.Id, "Discarded", ct);
            await _sender.EditTextAsync(chatId, cb.Message.MessageId, "Ok, not saving.", ct);
        }
    }

    private async Task<WebApplication1.Entities.User> UpsertUser(long chatId, CancellationToken ct)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.TelegramChatId == chatId, ct);
        if (user != null) return user;

        user = new WebApplication1.Entities.User
        {
            Id = Guid.NewGuid(),
            TelegramChatId = chatId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    private async Task HandlePhotoAsync(Guid userId, long chatId, Message message, CancellationToken ct)
    {
        var (fileId, bytes) = await _files.DownloadBestPhotoAsync(message, ct);

        var result = await _extractor.ExtractAsync(bytes, ct);

        var pending = new Product
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductName = result.ProductName,
            ExpiryDate = result.ExpiryDate?.Date,
            TelegramPhotoFileId = fileId,
            CreatedAtUtc = DateTime.UtcNow,
            Notes = result.Notes,
            Confidence = result.Confidence,
            Status = ProductStatus.Pending
        };

        _db.Products.Add(pending);
        await _db.SaveChangesAsync(ct);

        var expiryText = pending.ExpiryDate?.ToString("yyyy-MM-dd") ?? "unknown";

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Save", $"confirm:{pending.Id}"),
                InlineKeyboardButton.WithCallbackData("Discard", $"discard:{pending.Id}")
            }
        });

        await _sender.SendTextAsync(chatId,
            $"Detected:\nName: {pending.ProductName}\nExpiry: {expiryText}\nConfidence: {pending.Confidence:0.00}",
            ct,
            keyboard);
    }

    private async Task DeleteByCommand(Guid userId, long chatId, string text, CancellationToken ct)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !Guid.TryParse(parts[1], out var id))
        {
            await _sender.SendTextAsync(chatId, "Format: /delete <guid>", ct);
            return;
        }

        var product = await _db.Products.FirstOrDefaultAsync(p =>
            p.Id == id && p.UserId == userId && p.Status == ProductStatus.Confirmed, ct);

        if (product == null)
        {
            await _sender.SendTextAsync(chatId, "Not found.", ct);
            return;
        }

        _db.Products.Remove(product);
        await _db.SaveChangesAsync(ct);

        await _sender.SendTextAsync(chatId, $"Deleted: {product.ProductName}", ct);
    }
}
