using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;


var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();   // опционально, но полезно для чистоты
builder.Logging.AddConsole();        // ОБЯЗАТЕЛЬНО для Console.WriteLine и ILogger
builder.Logging.SetMinimumLevel(LogLevel.Information);
static string BuildNpgsqlFromCopilotSecret(string secretJson)
{
    using var doc = JsonDocument.Parse(secretJson);
    var root = doc.RootElement;

    string host = root.GetProperty("host").GetString()!;
    int port = root.TryGetProperty("port", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 5432;

    // Copilot обычно использует dbname, иногда database.
    string db = root.TryGetProperty("dbname", out var d) ? d.GetString()! :
        root.TryGetProperty("database", out var db2) ? db2.GetString()! :
        "postgres";

    // Copilot обычно username/password, иногда user.
    string user = root.TryGetProperty("username", out var u) ? u.GetString()! :
        root.TryGetProperty("user", out var u2) ? u2.GetString()! :
        throw new InvalidOperationException("DB secret JSON missing 'username'.");

    string pass = root.GetProperty("password").GetString()!;

    return $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true;";
}
var cs = builder.Configuration.GetConnectionString("Postgres");

if (!string.IsNullOrWhiteSpace(cs) && cs.TrimStart().StartsWith("{"))
{
    builder.Configuration["ConnectionStrings:Postgres"] = BuildNpgsqlFromCopilotSecret(cs);
}

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
builder.Services.Configure<HostOptions>(opts =>
{
    opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});


builder.Services.AddHostedService<ExpiryNotifierHostedService>();
builder.Services.AddScoped<IUpdateDispatcher, UpdateDispatcher>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ITelegramSender, TelegramSender>();
builder.Services.AddScoped<TelegramFileService>();
builder.Services.AddHostedService<TelegramPollingService>();

builder.Services.AddScoped<IOpenAiProductExtractor, OpenAiProductExtractor>();

builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddDbContextCheck<AppDbContext>("db");

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.MapControllers();

// ALB healthcheck (always 200 if app is alive)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = r => r.Name == "self"
});

// Optional deep healthcheck
app.MapHealthChecks("/health/db", new HealthCheckOptions
{
    Predicate = r => r.Name == "db",
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var result = new
        {
            status = report.Status.ToString(),
            entries = report.Entries.ToDictionary(
                e => e.Key,
                e => new {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    error = e.Value.Exception?.Message
                })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(result));
    }
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}


app.Run();