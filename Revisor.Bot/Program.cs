using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;


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
builder.Services.AddScoped<IUpdateDispatcher, UpdateDispatcher>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ITelegramSender, TelegramSender>();
builder.Services.AddScoped<TelegramFileService>();
builder.Services.AddScoped<IOpenAiProductExtractor, OpenAiProductExtractor>();

builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();

app.Run();

