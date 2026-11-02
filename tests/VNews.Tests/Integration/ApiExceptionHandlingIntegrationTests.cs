using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VNews.Application.Common;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Domain.Identity;
using VNews.Infrastructure.Persistence;
using VNews.Infrastructure.Services;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>Unexpected failures behind /api become sanitized ProblemDetails with a findable trace id.</summary>
public class ApiExceptionHandlingIntegrationTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public ApiExceptionHandlingIntegrationTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private HttpClient ClientWithFailingQueryService() => _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
    {
        services.AddScoped<ArticleQueryService>();
        services.AddScoped<IArticleQueryService, FaultingQueryService>();
    })).CreateClient();

    [Fact]
    public async Task UnexpectedException_ReturnsGenericProblemDetails_WithoutLeakingInternals()
    {
        var response = await ClientWithFailingQueryService().GetAsync("/api/v1/articles/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SECRET-CONNECTION-STRING", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain(" at VNews", body);
        Assert.Contains("traceId", body);
        Assert.Contains("500", body);

        // The operator can find the real exception by trace id.
        var traceId = System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("traceId").GetString();
        Assert.Contains(_factory.Logs.Entries, e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException
            && e.Exception.Message.Contains("SECRET-CONNECTION-STRING") && e.Message.Contains(traceId!));
    }

    [Fact]
    public async Task NotFoundException_MapsTo404_WithSafeMessage()
    {
        var response = await ClientWithFailingQueryService().GetAsync("/api/v1/articles/missing-entity");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Không tìm thấy bài viết #42", await response.Content.ReadAsStringAsync());
    }

    private sealed class FaultingQueryService : IArticleQueryService
    {
        private readonly ArticleQueryService _inner;
        public FaultingQueryService(ArticleQueryService inner) => _inner = inner;

        public Task<ArticleDetailDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            if (slug == "boom") throw new InvalidOperationException("SECRET-CONNECTION-STRING=Server=prod;Password=hunter2");
            if (slug == "missing-entity") throw new ArticleNotFoundException(42);
            return _inner.GetBySlugAsync(slug, cancellationToken);
        }

        public Task<PagedResult<ArticleSummaryDto>> GetByCategoryAsync(string categorySlug, int page, int pageSize, CancellationToken cancellationToken = default, IReadOnlyCollection<int>? excludeIds = null) => _inner.GetByCategoryAsync(categorySlug, page, pageSize, cancellationToken, excludeIds);
        public Task<PagedResult<ArticleSummaryDto>> SearchAsync(string query, int page, int pageSize, CancellationToken cancellationToken = default, VNews.Application.Common.SearchFilter? filter = null) => _inner.SearchAsync(query, page, pageSize, cancellationToken, filter);
        public Task<PagedResult<ArticleSummaryDto>> GetByTagAsync(string tagSlug, int page, int pageSize, CancellationToken cancellationToken = default) => _inner.GetByTagAsync(tagSlug, page, pageSize, cancellationToken);
        public Task<PagedResult<ArticleSummaryDto>> GetLatestAsync(int page, int pageSize, CancellationToken cancellationToken = default) => _inner.GetLatestAsync(page, pageSize, cancellationToken);
        public Task<IReadOnlyList<ArticleSummaryDto>> GetMostViewedAsync(int count, int? excludeArticleId = null, CancellationToken cancellationToken = default) => _inner.GetMostViewedAsync(count, excludeArticleId, cancellationToken);
        public Task<ArticleNeighborsDto> GetNeighborsAsync(int articleId, CancellationToken cancellationToken = default) => _inner.GetNeighborsAsync(articleId, cancellationToken);
        public Task<IReadOnlyList<CategoryGroupDto>> GetSubCategoryGroupsAsync(string parentSlug, int perGroup, IReadOnlyCollection<int> excludeIds, CancellationToken cancellationToken = default) => _inner.GetSubCategoryGroupsAsync(parentSlug, perGroup, excludeIds, cancellationToken);
    }
}
