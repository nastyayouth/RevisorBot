using Telegram.Bot.Types;

public interface IUpdateDispatcher
{
    Task HandleAsync(Update update, CancellationToken ct);
}