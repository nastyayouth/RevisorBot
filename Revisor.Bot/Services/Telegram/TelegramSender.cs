using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;



public class TelegramSender : ITelegramSender
{
    private readonly ITelegramBotClient _bot;

    public TelegramSender(ITelegramBotClient bot) => _bot = bot;

    public Task SendTextAsync(long chatId, string text, CancellationToken ct, ReplyMarkup? markup = null) =>
        _bot.SendMessage(chatId, text, replyMarkup: markup, cancellationToken: ct);

    public Task EditTextAsync(long chatId, int messageId, string text, CancellationToken ct) =>
        _bot.EditMessageText(chatId, messageId, text, cancellationToken: ct);

    public Task AnswerCallbackAsync(string callbackQueryId, string text, CancellationToken ct) =>
        _bot.AnswerCallbackQuery(callbackQueryId, text, cancellationToken: ct);
}