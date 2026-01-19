using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

public class ExpiryNotifierHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotClient _bot;
    private readonly ILogger<ExpiryNotifierHostedService> _logger;
  

    public ExpiryNotifierHostedService(
        IServiceScopeFactory scopeFactory,
        ITelegramBotClient bot,
        ILogger<ExpiryNotifierHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _bot = bot;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // первый запуск сразу
            await CheckAsync(stoppingToken);

           
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Expiry notifier first run failed");
        }
        
        // затем — раз в 7 дней
        var timer = new PeriodicTimer(TimeSpan.FromDays(7));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Expiry notifier run failed");
            }
        }

 
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(
            now.Year,
            now.Month,
            1,
            0, 0, 0,
            DateTimeKind.Utc
        );
        var startOfNextMonth = startOfMonth.AddMonths(1);
        var monthKey = $"{now:yyyy-MM}";

        var products = await db.Products
            .Include(p => p.User)
            .Where(p =>
                p.ExpiryDate != null &&
                p.ExpiryDate >= startOfMonth &&
                p.ExpiryDate < startOfNextMonth &&
                p.NotifiedForMonth != monthKey)
            .ToListAsync(ct);

        if (products.Count == 0)
            return;

        var groupedByUser = products.GroupBy(p => p.User.TelegramChatId);

        foreach (var group in groupedByUser)
        {
            var messageLines = group
                .OrderBy(p => p.ExpiryDate)
                .Select(p => $"• {p.ProductName} — до {p.ExpiryDate:yyyy-MM-dd}");

            var text =
                "⚠️ В этом месяце истекает срок годности:\n\n" +
                string.Join("\n", messageLines);

            await _bot.SendMessage(
                chatId: group.Key,
                text: text,
                cancellationToken: ct);
        }

        foreach (var product in products)
        {
            product.NotifiedForMonth = monthKey;
        }

        await db.SaveChangesAsync(ct);
    }
}
