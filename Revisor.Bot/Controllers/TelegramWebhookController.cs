using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;

[ApiController]
[Route("telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly IUpdateDispatcher _dispatcher;

    public TelegramWebhookController(IUpdateDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpPost("update")]
    public async Task<IActionResult> Update([FromBody] Update update, CancellationToken ct)
    {
        await _dispatcher.HandleAsync(update, ct);
        return Ok();
    }
}