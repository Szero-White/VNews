using System.Security.Cryptography;

namespace VNews.Web.Middleware;

/// <summary>
/// Adds browser-side protections to every response.
/// Scripts: only same-origin files plus inline scripts carrying the per-request nonce
/// (<see cref="NonceItemKey"/>); inline event handlers and remote scripts are blocked.
/// Styles keep 'unsafe-inline' because the existing views use many inline style attributes.
/// Anything served from /uploads gets a locked-down policy so even a hostile file opened
/// directly cannot run script.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    public const string NonceItemKey = "csp-nonce";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[NonceItemKey] = nonce;
        // Decided up front: status-code re-execution rewrites Request.Path before headers are sent.
        var isUpload = context.Request.Path.StartsWithSegments("/uploads");

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["X-Trace-Id"] = context.TraceIdentifier;

            headers["Content-Security-Policy"] = isUpload
                ? "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox"
                : "default-src 'self'; " +
                  $"script-src 'self' 'nonce-{nonce}'; " +
                  "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                  "font-src 'self' https://fonts.gstatic.com; " +
                  "img-src 'self' data: https:; " +
                  "connect-src 'self'; " +
                  "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
            return Task.CompletedTask;
        });

        return _next(context);
    }
}
