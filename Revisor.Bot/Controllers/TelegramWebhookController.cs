using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;

[ApiController]
[Route("telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly IUpdateDispatcher _dispatcher;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public TelegramWebhookController(IUpdateDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
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