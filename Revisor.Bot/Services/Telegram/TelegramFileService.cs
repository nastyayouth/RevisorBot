using Telegram.Bot;
using Telegram.Bot.Types;

public class TelegramFileService
{
    private readonly ITelegramBotClient _bot;

    public TelegramFileService(ITelegramBotClient bot) => _bot = bot;

    public async Task<(string fileId, byte[] bytes)> DownloadBestPhotoAsync(Message message, CancellationToken ct)
    {
        var best = message.Photo!.OrderBy(p => p.FileSize).Last();
        var file = await _bot.GetFile(best.FileId, ct);

        await using var ms = new MemoryStream();
        await _bot.DownloadFile(file.FilePath!, ms, ct);
        return (best.FileId, ms.ToArray());
    }
}