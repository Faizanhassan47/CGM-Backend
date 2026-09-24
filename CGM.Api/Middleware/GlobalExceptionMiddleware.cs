using System.Security.Claims;

namespace CGM.Api.Middleware;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request cancelled: {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        catch (Exception exception)
        {
            var errorId = Guid.NewGuid().ToString("N");
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub")
                ?? "anonymous";

            logger.LogError(exception,
                "Unhandled API exception. ErrorId={ErrorId} Method={Method} Path={Path} UserId={UserId} Timestamp={Timestamp}",
                errorId, context.Request.Method, context.Request.Path, userId, DateTimeOffset.UtcNow);

            Console.WriteLine($"[GlobalException] {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
            if (exception.InnerException != null)
            {
                Console.WriteLine($"[GlobalException.Inner] {exception.InnerException.GetType().Name}: {exception.InnerException.Message}");
            }

            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "Unexpected server error",
                errorId
            });
        }
    }
}
