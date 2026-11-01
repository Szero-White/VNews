using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace VNews.Web.Startup;

public static class RateLimitingExtensions
{
    /// <summary>
    /// Brute-force protection for sign-in / registration POSTs, per client IP (policy "auth"). Behind a reverse proxy, enable
    /// ForwardedHeaders so RemoteIpAddress is the real client. Limits come from RateLimiting:Auth:PermitLimit / WindowSeconds.
    /// </summary>
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimiting:Auth:PermitLimit", 10);
        var windowSeconds = configuration.GetValue("RateLimiting:Auth:WindowSeconds", 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0
                }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("VNews.RateLimiting");
                logger.LogWarning("Rate limit hit on {Path} from {RemoteIp}.", context.HttpContext.Request.Path.Value, context.HttpContext.Connection.RemoteIpAddress);
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }
                context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                await context.HttpContext.Response.WriteAsync("Bạn thao tác quá nhanh. Vui lòng đợi một lúc rồi thử lại.", cancellationToken);
            };
        });
        return services;
    }
}
