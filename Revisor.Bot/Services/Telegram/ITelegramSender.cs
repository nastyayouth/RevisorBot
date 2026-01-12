using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

public interface ITelegramSender
{
    Task SendTextAsync(long chatId, string text, CancellationToken ct, ReplyMarkup? markup = null);
    Task EditTextAsync(long chatId, int messageId, string text, CancellationToken ct);
    Task AnswerCallbackAsync(string callbackQueryId, string text, CancellationToken ct);
}