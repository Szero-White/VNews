using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Domain.Identity;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>
/// Authorization checked with the real HTTP requests a hostile user could send (bypassing every button), always
/// followed by a look at the database to prove nothing changed. Real accounts, isolated database.
/// </summary>
public sealed class RolePermissionRequestTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public RolePermissionRequestTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private static void AssertDenied(HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/tai-khoan/khong-co-quyen", r.Headers.Location!.ToString());
    }

    private async Task<int> PendingArticleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var svc = scope.ServiceProvider.GetRequiredService<IArticleCommandService>();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var author = (await users.FindByEmailAsync("author@vnews.local"))!;
        var id = await svc.CreateDraftAsync(new ArticleSaveRequest { Title = "Bài kiểm tra quyền " + Guid.NewGuid().ToString("N")[..6], Summary = "Tóm tắt", Body = "<p>Nội dung</p>", CategoryId = await db.Categories.Select(c => c.Id).FirstAsync() }, author.Id, author.DisplayName);
        await svc.SubmitForReviewAsync(id, author.Id);
        return id;
    }

    private async Task<ArticleStatus> StatusAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Articles.AsNoTracking().FirstAsync(a => a.Id == id)).Status;
    }

    private async Task<ApplicationUser> NewUserAsync(string role)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Người dùng thử", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "TestPassword123!")).Succeeded);
        await users.AddToRoleAsync(user, role);
        return user;
    }

    [Fact]
    public async Task Author_CannotPublishOrRejectAnArticle_AndItStaysPending()
    {
        var id = await PendingArticleAsync();
        var author = await _factory.LoginCmsAsync("author@vnews.local");

        AssertDenied(await author.PostFormAsync($"/cms/CmsArticles/Publish/{id}", "/cms", new()));
        AssertDenied(await author.PostFormAsync($"/cms/CmsArticles/Reject/{id}", "/cms", new() { ["reason"] = "Tự từ chối" }));

        Assert.Equal(ArticleStatus.PendingReview, await StatusAsync(id));
    }

    [Fact]
    public async Task Reader_CannotUseAnyNewsroomEndpoint()
    {
        var id = await PendingArticleAsync();
        var email = (await NewUserAsync(Roles.Reader)).Email!;
        var reader = await _factory.LoginCmsAsync(email, "TestPassword123!");

        foreach (var path in new[] { "/cms", "/cms/CmsArticles/Create", "/cms/CmsOverview", "/cms/CmsAudit", "/cms/CmsUsers", "/cms/CmsCategories" })
            AssertDenied(await reader.GetAsync(path));

        // The profile page is the one place a reader may POST to; use its token for the forged requests.
        AssertDenied(await reader.PostFormAsync($"/cms/CmsArticles/Publish/{id}", "/tai-khoan/ho-so", new()));
        AssertDenied(await reader.PostFormAsync($"/cms/CmsArticles/SubmitForReview/{id}", "/tai-khoan/ho-so", new()));
        Assert.Equal(ArticleStatus.PendingReview, await StatusAsync(id));
    }

    [Fact]
    public async Task Editor_CannotManageUsersOrCategories_ByDirectRequest()
    {
        var target = await NewUserAsync(Roles.Reader);
        var editor = await _factory.LoginCmsAsync("editor@vnews.local");

        AssertDenied(await editor.PostFormAsync("/cms/CmsUsers/SetRole", "/cms", new() { ["userId"] = target.Id, ["role"] = Roles.Admin }));
        AssertDenied(await editor.PostFormAsync("/cms/CmsCategories/Create", "/cms", new() { ["Name"] = "Lén tạo", ["Slug"] = "len-tao", ["DisplayOrder"] = "1" }));

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(new[] { Roles.Reader }, await users.GetRolesAsync((await users.FindByIdAsync(target.Id))!));
        Assert.False(await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Categories.AnyAsync(c => c.Slug == "len-tao"));
    }

    [Fact]
    public async Task Author_CannotChangeRoles_EvenOwn()
    {
        string authorId;
        using (var scope = _factory.Services.CreateScope())
            authorId = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("author@vnews.local"))!.Id;
        var author = await _factory.LoginCmsAsync("author@vnews.local");

        AssertDenied(await author.PostFormAsync("/cms/CmsUsers/SetRole", "/cms", new() { ["userId"] = authorId, ["role"] = Roles.Admin }));

        using var check = _factory.Services.CreateScope();
        var users = check.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(new[] { Roles.Author }, await users.GetRolesAsync((await users.FindByIdAsync(authorId))!));
    }

    [Fact]
    public async Task Author_CannotEditAnotherAuthorsArticle_ByDirectPost()
    {
        var other = await NewUserAsync(Roles.Author);
        int id;
        string rowVersion;
        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IArticleCommandService>();
            var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
            id = await svc.CreateDraftAsync(new ArticleSaveRequest { Title = "Bài gốc của người khác", Summary = "Tóm tắt", Body = "<p>Gốc</p>", CategoryId = await db.Categories.Select(c => c.Id).FirstAsync() }, other.Id, other.DisplayName);
            rowVersion = Convert.ToBase64String((await db.Articles.AsNoTracking().FirstAsync(a => a.Id == id)).RowVersion);
        }
        var author = await _factory.LoginCmsAsync("author@vnews.local");

        var response = await author.PostFormAsync($"/cms/CmsArticles/Edit/{id}", "/cms/CmsArticles/Create", new()
        {
            ["Title"] = "Bị sửa trái phép", ["Summary"] = "x", ["Body"] = "<p>Hack</p>", ["CategoryId"] = "1", ["rowVersionBase64"] = rowVersion,
        });

        AssertDenied(response);
        using var check = _factory.Services.CreateScope();
        Assert.Equal("Bài gốc của người khác", (await check.ServiceProvider.GetRequiredService<VNewsDbContext>().Articles.AsNoTracking().FirstAsync(a => a.Id == id)).Title);
    }

    [Fact]
    public async Task Editor_RejectWithReason_RecordsItAndShowsItEncodedToTheAuthor()
    {
        var id = await PendingArticleAsync();
        var editor = await _factory.LoginCmsAsync("editor@vnews.local");
        var reason = "Thiếu nguồn <script>alert(1)</script>";

        var response = await editor.PostFormAsync($"/cms/CmsArticles/Reject/{id}", "/cms", new() { ["reason"] = reason });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(ArticleStatus.Rejected, await StatusAsync(id));

        var html = await (await _factory.LoginCmsAsync("author@vnews.local")).GetStringAsync("/cms?status=Rejected");
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("Thiếu nguồn", WebUtility.HtmlDecode(html));
    }

    [Fact]
    public async Task Admin_CannotDemoteTheLastAdministrator_AndCanDemoteAnotherWhenTwoExist()
    {
        var second = await NewUserAsync(Roles.Admin);
        string firstId;
        using (var scope = _factory.Services.CreateScope())
            firstId = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("admin@vnews.local"))!.Id;
        var first = await _factory.LoginCmsAsync("admin@vnews.local");
        var secondClient = await _factory.LoginCmsAsync(second.Email!, "TestPassword123!");

        // Two administrators: the first demotes the second (allowed).
        await first.PostFormAsync("/cms/CmsUsers/SetRole", "/cms/CmsUsers", new() { ["userId"] = second.Id, ["role"] = Roles.Editor });
        // The demoted account lost the admin area at once, on its very next request (its old cookie is no longer valid).
        var afterDemotion = await secondClient.GetAsync("/cms/CmsUsers");
        Assert.Equal(HttpStatusCode.Redirect, afterDemotion.StatusCode);
        Assert.Contains("/tai-khoan/dang-nhap", afterDemotion.Headers.Location!.ToString());

        // Only one administrator is left: it cannot be demoted by anyone (not by itself, not by a forged request).
        await first.PostFormAsync("/cms/CmsUsers/SetRole", "/cms/CmsUsers", new() { ["userId"] = firstId, ["role"] = Roles.Reader });
        using var check = _factory.Services.CreateScope();
        var users = check.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Contains(Roles.Admin, await users.GetRolesAsync((await users.FindByIdAsync(firstId))!));
        Assert.Single(await users.GetUsersInRoleAsync(Roles.Admin), u => u.Id == firstId);
    }

    [Fact]
    public async Task ForgedPosts_WithoutAntiforgeryToken_AreRefused()
    {
        var id = await PendingArticleAsync();
        var editor = await _factory.LoginCmsAsync("editor@vnews.local");

        var response = await editor.PostAsync($"/cms/CmsArticles/Publish/{id}", new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ArticleStatus.PendingReview, await StatusAsync(id));
    }

    [Fact]
    public async Task CoverUpload_IsStaffOnly_AndRefusesSvgAndNonImages_OnTheServer()
    {
        var reader = await _factory.LoginCmsAsync((await NewUserAsync(Roles.Reader)).Email!, "TestPassword123!");
        var denied = await reader.PostAsync("/cms/CmsArticles/UploadCoverImage", new MultipartFormDataContent { { new ByteArrayContent(new byte[] { 1 }), "file", "a.png" } });
        Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);

        var author = await _factory.LoginCmsAsync("author@vnews.local");
        var token = await HttpHelpers.TokenFromAsync(await author.GetAsync("/cms/CmsArticles/Create"));
        async Task<HttpResponseMessage> Upload(string name, string type, byte[] bytes)
        {
            var content = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(type);
            content.Add(file, "file", name);
            var request = new HttpRequestMessage(HttpMethod.Post, "/cms/CmsArticles/UploadCoverImage") { Content = content };
            request.Headers.Add("RequestVerificationToken", token);
            return await author.SendAsync(request);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload("x.svg", "image/svg+xml", System.Text.Encoding.UTF8.GetBytes("<svg onload=alert(1)/>"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload("x.png", "image/png", System.Text.Encoding.UTF8.GetBytes("not really a png"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload("x.png", "image/png", new byte[3 * 1024 * 1024 + 10])).StatusCode);
    }
}
