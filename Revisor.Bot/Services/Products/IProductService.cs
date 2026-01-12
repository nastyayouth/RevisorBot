using Telegram.Bot.Types;

public interface IProductService
{
    Task HandleMessageAsync(Message message, CancellationToken ct);
    Task HandleCallbackAsync(CallbackQuery cb, CancellationToken ct);
}