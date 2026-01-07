using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using WebApplication1.Entities;
using User = Telegram.Bot.Types.User;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddSingleton<ITelegramBotClient>(_ =>
{
    var token = builder.Configuration["Telegram:BotToken"]!;
    return new TelegramBotClient(token);
});

builder.Services.AddHttpClient("openai", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/v1/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["OpenAI:ApiKey"]!);
});

builder.Services.AddHostedService<ExpiryNotifierHostedService>();

var app = builder.Build();

app.MapPost("/telegram/update", async (
    Update update,
    ITelegramBotClient bot,
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration cfg) =>
{
    // ===== 0) Callback buttons (confirm/discard) =====
    if (update.CallbackQuery != null)
    {
        var cb = update.CallbackQuery;
        var chatId = cb.Message!.Chat.Id;
        var data = cb.Data ?? "";

        // Upsert user (для безопасности владения)
        var userCb = await db.Users.SingleOrDefaultAsync(x => x.TelegramChatId == chatId);
        if (userCb == null)
        {
            userCb = new WebApplication1.Entities.User
            {
                Id = Guid.NewGuid(),
                TelegramChatId = chatId,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Users.Add(userCb);
            await db.SaveChangesAsync();
        }

        if (data.StartsWith("confirm:", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(data["confirm:".Length..], out var id))
            {
                await bot.AnswerCallbackQuery(cb.Id, "Некорректный id");
                return Results.Ok();
            }

            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userCb.Id);
            if (product == null)
            {
                await bot.AnswerCallbackQuery(cb.Id, "Не найдено");
                return Results.Ok();
            }

            product.Status = ProductStatus.Confirmed;
            await db.SaveChangesAsync();

            await bot.AnswerCallbackQuery(cb.Id, "Сохранено ✅");

            var expiryText = product.ExpiryDate?.ToString("yyyy-MM-dd") ?? "не удалось определить";
            await bot.EditMessageText(
                chatId: chatId,
                messageId: cb.Message.MessageId,
                text: $"✅ Сохранено!\nТовар: {product.ProductName}\nСрок годности: {expiryText}"
            );

            return Results.Ok();
        }

        if (data.StartsWith("discard:", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(data["discard:".Length..], out var id))
            {
                await bot.AnswerCallbackQuery(cb.Id, "Некорректный id");
                return Results.Ok();
            }

            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userCb.Id);
            if (product != null)
            {
                db.Products.Remove(product);
                await db.SaveChangesAsync();
            }

            await bot.AnswerCallbackQuery(cb.Id, "Не сохраняю ❌");

            await bot.EditMessageText(
                chatId: chatId,
                messageId: cb.Message.MessageId,
                text: "❌ Ок, не сохраняю. Пришли другое фото"
            );

            return Results.Ok();
        }

        await bot.AnswerCallbackQuery(cb.Id);
        return Results.Ok();
    }

    // ===== 1) Message handlers =====
    var message = update.Message;
    if (message == null)
        return Results.Ok();

    var chatIdMsg = message.Chat.Id;

    // Upsert user
    var user = await db.Users.SingleOrDefaultAsync(x => x.TelegramChatId == chatIdMsg);
    if (user == null)
    {
        user = new WebApplication1.Entities.User
        {
            Id = Guid.NewGuid(),
            TelegramChatId = chatIdMsg,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    // /start
    if (message.Text == "/start")
    {
        await bot.SendMessage(chatIdMsg,
            "Привет! 👋\n\nПришли фото продукта — я распознаю название и срок годности и попрошу подтверждение перед сохранением.\n\nКоманды:\n/list — список\n/delete <id> — удалить");
        return Results.Ok();
    }

    // /list
    if (message.Text == "/list")
    {
        var items = await db.Products
            .Where(p => p.UserId == user.Id && p.Status == ProductStatus.Confirmed)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(50)
            .ToListAsync();

        if (items.Count == 0)
        {
            await bot.SendMessage(chatIdMsg, "Список пуст. Пришли фото продукта");
            return Results.Ok();
        }

        var lines = items.Select(p =>
            $"{p.Id} | {p.ProductName} | {(p.ExpiryDate?.ToString("yyyy-MM-dd") ?? "—")}");

        await bot.SendMessage(chatIdMsg,
            "📦 Твои продукты:\n" + string.Join("\n", lines) + "\n\nУдаление: /delete <id>");
        return Results.Ok();
    }

    // /delete <guid>
    if (message.Text != null && message.Text.StartsWith("/delete", StringComparison.OrdinalIgnoreCase))
    {
        var parts = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !Guid.TryParse(parts[1], out var id))
        {
            await bot.SendMessage(chatIdMsg, "Формат: /delete <id>\nНапример: /delete 01234567-89ab-cdef-0123-456789abcdef");
            return Results.Ok();
        }

        var product = await db.Products.FirstOrDefaultAsync(p =>
            p.Id == id && p.UserId == user.Id && p.Status == ProductStatus.Confirmed);

        if (product == null)
        {
            await bot.SendMessage(chatIdMsg, "Не нашла продукт с таким id.");
            return Results.Ok();
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync();

        await bot.SendMessage(chatIdMsg, $"Удалено 🗑️: {product.ProductName}");
        return Results.Ok();
    }

    // ===== 2) Фото -> OpenAI -> Pending + confirmation =====
    if (message.Photo == null || message.Photo.Length == 0)
        return Results.Ok();

    // Берём самую большую фотку
    var bestPhoto = message.Photo.OrderBy(p => p.FileSize).Last();
    var file = await bot.GetFile(bestPhoto.FileId);

    await using var ms = new MemoryStream();
    await bot.DownloadFile(file.FilePath!, ms);
    var bytes = ms.ToArray();
    var base64 = Convert.ToBase64String(bytes);

    // OpenAI: image -> structured JSON
    var openAiModel = cfg["OpenAI:Model"] ?? "gpt-4o-mini";
    var openai = httpClientFactory.CreateClient("openai");

    var schema = new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            product_name = new { type = "string" },
            expiry_date = new
            {
                type = new object[] { "string", "null" },
                description = "Expiration date in YYYY-MM-DD, or null if unknown"
            },
            confidence = new { type = "number", minimum = 0, maximum = 1 },
            notes = new { type = new object[] { "string", "null" } }
        },
        required = new[] { "product_name", "expiry_date", "confidence", "notes" }
    };

    var requestBody = new
    {
        model = openAiModel,
        input = new object[]
        {
            new
            {
                role = "system",
                content =
                    "You extract product name (brand + type) and expiration date from packaging photos. " +
                    "Return ONLY structured output. If expiration is unclear, set expiry_date=null and explain in notes."
            },
            new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "input_text", text = "Find product name and expiration date on this image." },
                    new { type = "input_image", image_url = $"data:image/jpeg;base64,{base64}" }
                }
            }
        },
        text = new
        {
            format = new
            {
                type = "json_schema",
                name = "product_expiry_extraction",
                strict = true,
                schema = schema
            }
        }
    };

    using var resp = await openai.PostAsync(
        "responses",
        new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

    if (!resp.IsSuccessStatusCode)
    {
        var err = await resp.Content.ReadAsStringAsync();
        await bot.SendMessage(chatIdMsg, $"Не получилось распознать :( Ошибка OpenAI: {resp.StatusCode}\n{err}");
        return Results.Ok();
    }

    var json = await resp.Content.ReadAsStringAsync();
    var parsed = ExtractStructuredJsonFromResponses(json);

    if (parsed == null)
    {
        await bot.SendMessage(chatIdMsg, "Я получила ответ, но не смогла распарсить JSON. Попробуйте другое фото.");
        return Results.Ok();
    }

    var productName = parsed.Value.GetProperty("product_name").GetString() ?? "Unknown";

    DateTime? expiry = null;
    var expiryRaw = parsed.Value.GetProperty("expiry_date");
    if (expiryRaw.ValueKind == JsonValueKind.String &&
        DateTime.TryParse(expiryRaw.GetString(), out var dt))
    {
        // Если ExpiryDate в БД тип "date", Kind не важен, но Date всё равно ок
        expiry = dt.Date;
    }

    var confidence = parsed.Value.GetProperty("confidence").GetDouble();
    var notes = parsed.Value.GetProperty("notes").ValueKind == JsonValueKind.String
        ? parsed.Value.GetProperty("notes").GetString()
        : null;

    // SAVE as PENDING (важно!)
    var pending = new Product
    {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        ProductName = productName,
        ExpiryDate = expiry,
        TelegramPhotoFileId = bestPhoto.FileId,
        CreatedAtUtc = DateTime.UtcNow,
        NotifiedForMonth = null,
        Notes = notes,
        Confidence = confidence,
        Status = ProductStatus.Pending
    };

    db.Products.Add(pending);
    await db.SaveChangesAsync();

    var expiryTextPending = expiry.HasValue ? expiry.Value.ToString("yyyy-MM-dd") : "не удалось определить";

    var keyboard = new InlineKeyboardMarkup(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("✅ Save", $"confirm:{pending.Id}"),
            InlineKeyboardButton.WithCallbackData("❌ Discard", $"discard:{pending.Id}")
        }
    });

    await bot.SendMessage(
        chatIdMsg,
        $"Нашла вот так:\n\nТовар: {productName}\nСрок годности: {expiryTextPending}\nУверенность: {confidence:0.00}" +
        (string.IsNullOrWhiteSpace(notes) ? "" : $"\nКомментарий: {notes}") +
        "\n\nСохранить?",
        replyMarkup: keyboard);

    return Results.Ok();
});

app.MapGet("/health", () => Results.Ok("OK"));

app.Run();

static JsonElement? ExtractStructuredJsonFromResponses(string responsesApiRawJson)
{
    try
    {
        using var doc = JsonDocument.Parse(responsesApiRawJson);

        // Responses API: output -> [ { type:"message", content:[ { type:"output_text", text:"{...json...}" } ] } ]
        if (!doc.RootElement.TryGetProperty("output", out var output) ||
            output.ValueKind != JsonValueKind.Array ||
            output.GetArrayLength() == 0)
            return null;

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var c in content.EnumerateArray())
            {
                if (!c.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "output_text")
                    continue;

                if (!c.TryGetProperty("text", out var textEl) || textEl.ValueKind != JsonValueKind.String)
                    continue;

                var text = textEl.GetString();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // Второй проход: распарсить JSON-строку из поля text
                using var objDoc = JsonDocument.Parse(text);
                return objDoc.RootElement.Clone();
            }
        }

        return null;
    }
    catch
    {
        return null;
    }
}


