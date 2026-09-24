using System.Diagnostics;
using System.Security.Claims;
using CGM.Api.Services;

namespace CGM.Api.Middleware;

public sealed class RequestTelemetryMiddleware(RequestDelegate next, ILogger<RequestTelemetryMiddleware> logger, OperationalMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;

        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            var status = context.Response.StatusCode;
            var failed = status >= 500;
            metrics.RecordRequest(sw.ElapsedMilliseconds, failed);

            var method = context.Request.Method;
            var path = $"{context.Request.Path}{context.Request.QueryString}";
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Anonymous";

            var icon = status switch
            {
                >= 200 and < 300 => "🟢",
                >= 300 and < 400 => "🔵",
                >= 400 and < 500 => "🟡",
                _ => "🔴"
            };

            Console.ForegroundColor = status switch
            {
                >= 200 and < 300 => ConsoleColor.Green,
                >= 300 and < 400 => ConsoleColor.Cyan,
                >= 400 and < 500 => ConsoleColor.Yellow,
                _ => ConsoleColor.Red
            };

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [API HIT] {icon} {method,-6} {path} => {status} ({sw.ElapsedMilliseconds}ms) [User: {userId}]");
            Console.ResetColor();

            logger.LogDebug("HTTP {Method} {Path} => {StatusCode} in {ElapsedMs}ms UserId={UserId} CorrelationId={CorrelationId}",
                method, path, status, sw.ElapsedMilliseconds, userId, context.TraceIdentifier);
        }
    }
}
