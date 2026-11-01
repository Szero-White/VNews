using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using VNews.Application.Common;
using VNews.Application.DTOs.Cms;
using VNews.Web.Middleware;

namespace VNews.Tests.Unit;

public class ErrorMappingAndValidationTests
{
    [Theory]
    [InlineData(typeof(ArticleNotFoundException), 404)]
    [InlineData(typeof(CategoryNotFoundException), 404)]
    [InlineData(typeof(ArticleForbiddenException), 403)]
    [InlineData(typeof(ArticleConcurrencyException), 409)]
    [InlineData(typeof(ArticleWorkflowException), 409)]
    [InlineData(typeof(InvalidOperationException), 500)]
    public void ExceptionTypes_MapToExpectedStatus(Type type, int expected)
    {
        Exception ex = type switch
        {
            var t when t == typeof(ArticleNotFoundException) => new ArticleNotFoundException(1),
            var t when t == typeof(CategoryNotFoundException) => new CategoryNotFoundException(1),
            var t when t == typeof(ArticleForbiddenException) => new ArticleForbiddenException("x"),
            var t when t == typeof(ArticleConcurrencyException) => new ArticleConcurrencyException(),
            var t when t == typeof(ArticleWorkflowException) => new ArticleWorkflowException("x"),
            _ => new InvalidOperationException("boom")
        };

        Assert.Equal(expected, ApiExceptionHandler.Map(ex).Status);
    }

    [Fact]
    public async Task Handler_IgnoresNonApiRequests_SoMvcShowsItsFriendlyPage()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/cms/CmsArticles/Publish/1";

        var handled = await new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance)
            .TryHandleAsync(ctx, new InvalidOperationException("x"), default);

        Assert.False(handled);
    }

    [Fact]
    public async Task Handler_WritesProblemJson_WithoutExceptionMessageForServerErrors()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/v1/anything";
        ctx.Response.Body = new MemoryStream();

        var handled = await new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance)
            .TryHandleAsync(ctx, new InvalidOperationException("Password=hunter2"), default);

        Assert.True(handled);
        Assert.Equal(500, ctx.Response.StatusCode);
        ctx.Response.Body.Position = 0;
        var json = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("hunter2", json);
        using var doc = JsonDocument.Parse(json);
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("traceId").GetString()));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("/uploads/articles/abc.png", true)]
    [InlineData("/images/demo/x.jpg", true)]
    [InlineData("https://cdn.example.com/a.jpg", true)]
    [InlineData("http://cdn.example.com/a.jpg", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:image/svg+xml;base64,AAAA", false)]
    [InlineData("//evil.example/a.png", false)]
    [InlineData("/uploads/../../etc/passwd", false)]
    [InlineData("/uploads\\..\\x.png", false)]
    [InlineData("ftp://host/a.png", false)]
    [InlineData("relative/path.png", false)]
    public void CoverImageUrl_Rules(string? value, bool expected)
        => Assert.Equal(expected, ArticleSaveRequest.IsAcceptableCoverImageUrl(value));
}
