using Telegram.Bot.Types;

public class UpdateDispatcher : IUpdateDispatcher
{
    private readonly IProductService _products;
    private readonly ITelegramSender _sender;

    public UpdateDispatcher(IProductService products, ITelegramSender sender)
    {
        _products = products;
        _sender = sender;
    }

    public async Task HandleAsync(Update update, CancellationToken ct)
    {
        if (update.CallbackQuery != null)
        {
            await _products.HandleCallbackAsync(update.CallbackQuery, ct);
            return;
        }

        if (update.Message != null)
        {
            await _products.HandleMessageAsync(update.Message, ct);
            return;
        }
    }
}