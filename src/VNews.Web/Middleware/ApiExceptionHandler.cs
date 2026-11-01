using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Common;

namespace VNews.Web.Middleware;

/// <summary>
/// Turns exceptions that escape a /api request into RFC 7807 ProblemDetails with a stable status
/// code. The response never contains the exception message of unexpected errors, a stack trace or
/// inner exceptions — only a generic title and the trace id, which is also written to the log so
/// an operator can find the full exception.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) => _logger = logger;

    public static (int Status, string Title) Map(Exception exception) => exception switch
    {
        EntityNotFoundException => (StatusCodes.Status404NotFound, "Không tìm thấy dữ liệu"),
        ArticleForbiddenException => (StatusCodes.Status403Forbidden, "Không có quyền thực hiện thao tác"),
        ArticleConcurrencyException => (StatusCodes.Status409Conflict, "Dữ liệu đã bị thay đổi"),
        ArticleWorkflowException => (StatusCodes.Status409Conflict, "Thao tác không hợp lệ với trạng thái hiện tại"),
        BadHttpRequestException => (StatusCodes.Status400BadRequest, "Yêu cầu không hợp lệ"),
        _ => (StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi máy chủ")
    };

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Path.StartsWithSegments("/api"))
        {
            // MVC requests are rendered by the friendly /Home/Error page instead.
            return false;
        }

        var (status, title) = Map(exception);
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (status >= 500)
        {
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}. TraceId {TraceId}.", httpContext.Request.Method, httpContext.Request.Path.Value, traceId);
        }
        else
        {
            _logger.LogWarning("Handled {ExceptionType} on {Method} {Path} -> {Status}. TraceId {TraceId}.", exception.GetType().Name, httpContext.Request.Method, httpContext.Request.Path.Value, status, traceId);
        }

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            // Domain exceptions carry user-facing Vietnamese messages written by us; unexpected ones never do.
            Detail = status < 500 ? exception.Message : null,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = traceId;

        await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }
}
