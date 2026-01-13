using System.Diagnostics;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var sw = Stopwatch.StartNew();

        await _next(context);

        sw.Stop();

        // логируем только важные маршруты, чтобы не шуметь
        if (context.Request.Path.StartsWithSegments("/telegram"))
        {
            Console.WriteLine($"[REQ] {context.Request.Method} {context.Request.Path}");
            _logger.LogInformation("HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds);
        }
    }
}