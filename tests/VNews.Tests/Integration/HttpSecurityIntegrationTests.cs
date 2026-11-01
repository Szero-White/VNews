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

/// <summary>Security headers, cookies, newsroom sign-in, MVC error handling and logging, over real HTTP.</summary>
public class HttpSecurityIntegrationTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public HttpSecurityIntegrationTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    // ---------- security headers ----------

    [Theory]
    [InlineData("/")]
    [InlineData("/cms/dang-nhap")]
    [InlineData("/api/v1/categories")]
    [InlineData("/khong-ton-tai-123")]
    public async Task EveryResponse_CarriesSecurityHeaders(string path)
    {
        var response = await _factory.NewClient().GetAsync(path);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.True(response.Headers.Contains("Permissions-Policy"));
        Assert.False(string.IsNullOrWhiteSpace(response.Headers.GetValues("X-Trace-Id").Single()));

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("script-src 'self' 'unsafe-inline'", csp);
        Assert.DoesNotContain("'unsafe-eval'", csp);
    }

    [Fact]
    public async Task Csp_NonceIsFreshPerRequest_AndMatchesInlineScriptsOnCmsPages()
    {
        var client = await _factory.LoginCmsAsync("author@vnews.local");

        var first = await client.GetAsync("/cms/CmsArticles/Create");
        var second = await client.GetAsync("/cms/CmsArticles/Create");

        string Nonce(HttpResponseMessage r) =>
            Regex.Match(r.Headers.GetValues("Content-Security-Policy").Single(), "'nonce-([^']+)'").Groups[1].Value;

        var nonce1 = Nonce(first);
        Assert.False(string.IsNullOrEmpty(nonce1));
        Assert.NotEqual(nonce1, Nonce(second));

        var html = await first.Content.ReadAsStringAsync();
        Assert.Contains($"<script nonce=\"{nonce1}\">", html);
        // No inline event handlers remain that the policy would block.
        Assert.DoesNotMatch(@"\son[a-z]+\s*=\s*""", html);
    }

    [Fact]
    public async Task RejectButton_NoLongerUsesBlockedInlineHandler()
    {
        var client = await _factory.LoginCmsAsync("editor@vnews.local");
        var html = await (await client.GetAsync("/cms")).Content.ReadAsStringAsync();

        Assert.DoesNotMatch(@"\son[a-z]+\s*=\s*""", html); // no inline handler (onsubmit/onchange/...) the CSP would block
    }

    [Fact]
    public async Task Uploads_AreServedWithLockedDownPolicy()
    {
        var env = _factory.Services.GetRequiredService<IWebHostEnvironment>();
        var dir = Path.Combine(env.WebRootPath, "uploads", "articles");
        Directory.CreateDirectory(dir);
        var name = $"sectest-{Guid.NewGuid():N}.png";
        var path = Path.Combine(dir, name);
        await File.WriteAllBytesAsync(path, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 });
        try
        {
            var response = await _factory.NewClient().GetAsync($"/uploads/articles/{name}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var csp = response.Headers.GetValues("Content-Security-Policy").Single();
            Assert.Contains("default-src 'none'", csp);
            Assert.Contains("sandbox", csp);
            Assert.DoesNotContain("script-src", csp);
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        }
        finally { File.Delete(path); }
    }

    // ---------- cookies ----------

    [Fact]
    public async Task AuthCookie_IsHttpOnly_AndSameSiteLax()
    {
        var client = _factory.NewClient();
        var response = await client.PostFormAsync("/cms/dang-nhap", "/cms/dang-nhap",
            new Dictionary<string, string> { ["Email"] = "author@vnews.local", ["Password"] = "Demo@12345" });

        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith(".AspNetCore.Identity.Application=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- newsroom sign-in ----------

    [Fact]
    public async Task ReaderCredentials_OnCmsLogin_NeverReceiveAuthCookie()
    {
        var client = _factory.NewClient();
        var email = $"reader-{Guid.NewGuid():N}@example.test";
        var register = await client.PostFormAsync("/tai-khoan/dang-ky", "/tai-khoan/dang-ky", new Dictionary<string, string>
        {
            ["DisplayName"] = "Reader", ["Email"] = email, ["Password"] = "TestPassword123!", ["ConfirmPassword"] = "TestPassword123!"
        });
        Assert.Equal(HttpStatusCode.Redirect, register.StatusCode);

        var anonymous = _factory.NewClient(); // fresh client: no session carried over from registration
        var response = await anonymous.PostFormAsync("/cms/dang-nhap", "/cms/dang-nhap",
            new Dictionary<string, string> { ["Email"] = email, ["Password"] = "TestPassword123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(HttpHelpers.SetsAuthCookie(response), "a reader must not get an auth cookie from the newsroom login");
        Assert.Contains("không có quyền", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/cms")).StatusCode);
    }

    [Fact]
    public async Task StaffCredentials_OnCmsLogin_StillSignIn()
    {
        var client = await _factory.LoginCmsAsync("editor@vnews.local");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/cms")).StatusCode);
    }

    // ---------- MVC error handling ----------

    private async Task<int> SeedPendingArticle()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var article = new Article
        {
            Title = $"Cho duyet {Guid.NewGuid():N}",
            Slug = $"cho-duyet-{Guid.NewGuid():N}",
            Summary = "Tom tat bai cho duyet de kiem thu.",
            Body = "<p>Noi dung.</p>",
            Status = ArticleStatus.PendingReview,
            CategoryId = await db.Categories.Select(c => c.Id).FirstAsync(),
            AuthorName = "Tac gia",
            AuthorUserId = "someone",
            CreatedAt = DateTime.UtcNow
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return article.Id;
    }

    [Fact]
    public async Task Editor_PublishOrRejectMissingArticle_Returns404_NotAServerError()
    {
        var client = await _factory.LoginCmsAsync("editor@vnews.local");

        var publish = await client.PostFormAsync("/cms/CmsArticles/Publish/987654", "/cms", new());
        var reject = await client.PostFormAsync("/cms/CmsArticles/Reject/987654", "/cms", new() { ["reason"] = "ly do" });

        Assert.Equal(HttpStatusCode.NotFound, publish.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reject.StatusCode);
    }

    [Fact]
    public async Task Editor_RejectWithoutReason_IsRefused_AndArticleStaysPending()
    {
        var id = await SeedPendingArticle();
        var client = await _factory.LoginCmsAsync("editor@vnews.local");

        var response = await client.PostFormAsync($"/cms/CmsArticles/Reject/{id}", "/cms", new() { ["reason"] = "   " });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        Assert.Equal(ArticleStatus.PendingReview, (await db.Articles.AsNoTracking().FirstAsync(a => a.Id == id)).Status);
    }

    [Fact]
    public async Task EditPost_WithTamperedRowVersion_ShowsValidationMessage_NotAnException()
    {
        var client = await _factory.LoginCmsAsync("author@vnews.local");
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
            var svc = scope.ServiceProvider.GetRequiredService<IArticleCommandService>();
            var author = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("author@vnews.local");
            id = await svc.CreateDraftAsync(new()
            {
                Title = $"Ban nhap {Guid.NewGuid():N}", Summary = "Tom tat ban nhap kiem thu.",
                Body = "<p>Noi dung ban nhap du dai.</p>", CategoryId = await db.Categories.Select(c => c.Id).FirstAsync()
            }, author!.Id, "Tac gia demo");
        }

        var response = await client.PostFormAsync($"/cms/CmsArticles/Edit/{id}", $"/cms/CmsArticles/Edit/{id}", new()
        {
            ["Title"] = "Tieu de hop le moi", ["Summary"] = "Tom tat hop le moi cho bai viet.",
            ["Body"] = "<p>Noi dung hop le moi, du dai.</p>", ["CategoryId"] = "1",
            ["rowVersionBase64"] = "###not-base64###"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Phiên bản bài viết không hợp lệ", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task AnotherAuthor_CannotOpenOrEditSomeoneElsesDraft()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await users.FindByEmailAsync("author2@vnews.local") is null)
            {
                var u = new ApplicationUser { UserName = "author2@vnews.local", Email = "author2@vnews.local", DisplayName = "Tac gia 2", EmailConfirmed = true };
                Assert.True((await users.CreateAsync(u, "Demo@12345")).Succeeded);
                await users.AddToRoleAsync(u, Roles.Author);
            }
        }

        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
            var owner = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("author@vnews.local");
            id = await scope.ServiceProvider.GetRequiredService<IArticleCommandService>().CreateDraftAsync(new()
            {
                Title = $"Cua tac gia 1 {Guid.NewGuid():N}", Summary = "Tom tat bai cua tac gia thu nhat.",
                Body = "<p>Noi dung du dai cho bai viet.</p>", CategoryId = await db.Categories.Select(c => c.Id).FirstAsync()
            }, owner!.Id, "Tac gia demo");
        }

        var other = await _factory.LoginCmsAsync("author2@vnews.local");

        var open = await other.GetAsync($"/cms/CmsArticles/Edit/{id}");
        var submit = await other.PostFormAsync($"/cms/CmsArticles/SubmitForReview/{id}", "/cms", new());

        Assert.Equal(HttpStatusCode.Redirect, open.StatusCode);
        Assert.Contains("khong-co-quyen", open.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Redirect, submit.StatusCode);
        Assert.Contains("khong-co-quyen", submit.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("//evil.example/x.png")]
    [InlineData("/uploads/../../secret.png")]
    public async Task CoverImageUrl_WithDangerousValue_IsRejected_AndNothingIsSaved(string cover)
    {
        var client = await _factory.LoginCmsAsync("author@vnews.local");
        var title = $"Anh bia xau {Guid.NewGuid():N}";

        var response = await client.PostFormAsync("/cms/CmsArticles/Create", "/cms/CmsArticles/Create", new()
        {
            ["Title"] = title, ["Summary"] = "Tom tat bai viet kiem thu anh bia.",
            ["Body"] = "<p>Noi dung du dai cho bai viet.</p>", ["CategoryId"] = "1", ["CoverImageUrl"] = cover
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ảnh bìa phải là", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        using var scope = _factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Articles.AnyAsync(a => a.Title == title));
    }

    // ---------- logging ----------

    [Fact]
    public async Task Logs_NeverContainPasswords_AndRecordFailedSignIn()
    {
        var client = _factory.NewClient();
        await client.PostFormAsync("/cms/dang-nhap", "/cms/dang-nhap",
            new() { ["Email"] = "author@vnews.local", ["Password"] = "Wrong-Password-XYZ-987" });
        await _factory.LoginCmsAsync("author@vnews.local", "Demo@12345");

        var everything = string.Join("\n", _factory.Logs.Entries.Select(e => $"{e.Message} {e.State} {e.Exception}"));
        Assert.DoesNotContain("Wrong-Password-XYZ-987", everything);
        Assert.DoesNotContain("Demo@12345", everything);
        Assert.Contains(_factory.Logs.Entries, e => e.Level == LogLevel.Warning && e.Message.StartsWith("CMS sign-in failed"));
        Assert.Contains(_factory.Logs.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("signed in"));
    }
}

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

        public Task<HomePageData> GetHomePageDataAsync(CancellationToken cancellationToken = default) => _inner.GetHomePageDataAsync(cancellationToken);
        public Task<PagedResult<ArticleSummaryDto>> GetByCategoryAsync(string categorySlug, int page, int pageSize, CancellationToken cancellationToken = default) => _inner.GetByCategoryAsync(categorySlug, page, pageSize, cancellationToken);
        public Task<PagedResult<ArticleSummaryDto>> SearchAsync(string query, int page, int pageSize, CancellationToken cancellationToken = default) => _inner.SearchAsync(query, page, pageSize, cancellationToken);
        public Task<PagedResult<ArticleSummaryDto>> GetByTagAsync(string tagSlug, int page, int pageSize, CancellationToken cancellationToken = default) => _inner.GetByTagAsync(tagSlug, page, pageSize, cancellationToken);
        public Task<PagedResult<ArticleSummaryDto>> GetLatestAsync(int page, int pageSize, CancellationToken cancellationToken = default) => _inner.GetLatestAsync(page, pageSize, cancellationToken);
    }
}

/// <summary>Sign-in/registration POSTs are rate limited per client IP; normal page loads are not.</summary>
public class RateLimitingIntegrationTests : IClassFixture<RateLimitedWebApplicationFactory>
{
    private readonly RateLimitedWebApplicationFactory _factory;

    public RateLimitingIntegrationTests(RateLimitedWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task FourthSignInAttemptWithinTheWindow_Returns429WithRetryAfter()
    {
        var client = _factory.NewClient();
        var attempt = () => client.PostFormAsync("/tai-khoan/dang-nhap", "/tai-khoan/dang-nhap",
            new() { ["Email"] = "nobody@example.test", ["Password"] = "Wrong-Password-1" });

        for (var i = 1; i <= 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await attempt()).StatusCode);
        }

        var limited = await attempt();
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
        Assert.Contains("quá nhanh", await limited.Content.ReadAsStringAsync());
        Assert.Contains(_factory.Logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Rate limit hit"));

        // Reading pages is never limited, and the limit covers the CMS and registration forms too.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/tai-khoan/dang-nhap")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
    }
}

/// <summary>Hardening that only applies outside Development.</summary>
public class ProductionHardeningIntegrationTests : IClassFixture<ProductionWebApplicationFactory>
{
    private readonly ProductionWebApplicationFactory _factory;

    public ProductionHardeningIntegrationTests(ProductionWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void Cookies_AreSecureOnly_InProduction()
    {
        var cookies = _factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal(Microsoft.AspNetCore.Http.CookieSecurePolicy.Always, cookies.Cookie.SecurePolicy);
        Assert.True(cookies.Cookie.HttpOnly);
        Assert.Equal(Microsoft.AspNetCore.Http.SameSiteMode.Lax, cookies.Cookie.SameSite);
    }

    [Fact]
    public async Task ErrorPage_IsFriendlyVietnamese_WithTraceId_AndNoTechnicalDetail()
    {
        // Production forces Secure cookies, so requests must arrive over HTTPS (as they would behind TLS).
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/Home/Error");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Mã tra cứu lỗi", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("Development", html);
        Assert.DoesNotContain("StackTrace", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", html);
    }

    [Fact]
    public async Task DemoAccounts_AreNotSeeded_InProduction()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync("admin@vnews.local"));
    }
}
