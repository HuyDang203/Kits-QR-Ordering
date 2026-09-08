using System.Diagnostics;

namespace Annap.CoffeeQrOrdering.Web;

/// <summary>Development-only: logs method, path, status, and duration for every request.</summary>
internal sealed class DevRequestLoggingMiddleware(RequestDelegate next, ILogger<DevRequestLoggingMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            logger.LogInformation(
                "{Method} {Path}{QueryString} -> {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path.Value,
                context.Request.QueryString.Value,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds);
        }
    }
}
