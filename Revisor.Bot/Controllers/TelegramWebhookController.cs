using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;

[ApiController]
[Route("telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly IUpdateDispatcher _dispatcher;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(IUpdateDispatcher dispatcher,
        ILogger<TelegramWebhookController> logger
    )
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    [HttpPost("update")]
    public async Task<IActionResult> Update([FromBody] Update update, CancellationToken ct)
    {
        try
        {
            await _dispatcher.HandleAsync(update, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while handling telegram update");
            // не пробрасываем ошибку наружу
        }
        return Ok();

    }
}