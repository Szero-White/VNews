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
using VNews.Web.Helpers;

namespace VNews.Tests.Integration;

/// <summary>The role dashboards: what each role sees and may do, with real data in an isolated database.</summary>
public sealed class RoleDashboardIntegrationTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public RoleDashboardIntegrationTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private async Task<(int Id, string AuthorId)> CreateArticleAsync(string authorEmail, string title, bool submit)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var svc = scope.ServiceProvider.GetRequiredService<IArticleCommandService>();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var author = (await users.FindByEmailAsync(authorEmail))!;
        var categoryId = await db.Categories.Select(c => c.Id).FirstAsync();
        var id = await svc.CreateDraftAsync(new ArticleSaveRequest { Title = title, Summary = "Tóm tắt thử nghiệm", Body = "<p>Nội dung thử nghiệm.</p>", CategoryId = categoryId }, author.Id, author.DisplayName);
        if (submit) await svc.SubmitForReviewAsync(id, author.Id, false);
        return (id, author.Id);
    }

    // ---------- statistics, search, paging ----------

    [Fact]
    public async Task Stats_AreScopedToTheAuthor_AndMatchTheDatabase()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        await CreateArticleAsync("author@vnews.local", $"Nháp {marker}", submit: false);
        await CreateArticleAsync("author@vnews.local", $"Chờ {marker}", submit: true);

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IArticleCmsQueryService>();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var author = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("author@vnews.local"))!;

        var mine = await svc.GetCmsStatsAsync(author.Id);
        var all = await svc.GetCmsStatsAsync(null);

        Assert.Equal(await db.Articles.CountAsync(a => a.AuthorUserId == author.Id && a.Status == ArticleStatus.Draft), mine.Draft);
        Assert.Equal(await db.Articles.CountAsync(a => a.AuthorUserId == author.Id && a.Status == ArticleStatus.PendingReview), mine.PendingReview);
        Assert.Equal(await db.Articles.CountAsync(a => a.Status == ArticleStatus.Published), all.Published);
        Assert.Equal(all.Draft + all.PendingReview + all.Rejected + all.Published, all.Total);
        Assert.True(mine.Total <= all.Total);
    }

    [Fact]
    public async Task List_SearchesTitlesIgnoringAccents_AndPagesOnTheServer()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 3; i++) await CreateArticleAsync("author@vnews.local", $"Thời tiết đặc biệt {marker} số {i}", submit: false);

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IArticleCmsQueryService>();

        var found = await svc.ListForCmsAsync(null, null, 1, 15, search: $"thoi tiet dac biet {marker}");
        Assert.Equal(3, found.TotalCount);

        var firstPage = await svc.ListForCmsAsync(null, null, 1, 2, search: marker);
        var secondPage = await svc.ListForCmsAsync(null, null, 2, 2, search: marker);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Single(secondPage.Items);
        Assert.Empty(firstPage.Items.Select(i => i.Id).Intersect(secondPage.Items.Select(i => i.Id)));

        var none = await svc.ListForCmsAsync(null, null, 1, 15, search: "khong-co-bai-nao-nhu-the-" + marker);
        Assert.Equal(0, none.TotalCount);
    }

    [Fact]
    public async Task PendingQueue_CanBeListedOldestFirst()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        var first = await CreateArticleAsync("author@vnews.local", $"Cũ {marker}", submit: true);
        await Task.Delay(30);
        var second = await CreateArticleAsync("author@vnews.local", $"Mới {marker}", submit: true);

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IArticleCmsQueryService>();
        var oldestFirst = (await svc.ListForCmsAsync("PendingReview", null, 1, 50, search: marker, oldestFirst: true)).Items.Select(i => i.Id).ToList();

        Assert.Equal(new[] { first.Id, second.Id }, oldestFirst);
    }

    // ---------- what each role is offered ----------

    [Theory]
    [InlineData("Draft", true, false, true, true, false, false)]
    [InlineData("Draft", false, false, false, false, false, false)]
    [InlineData("Rejected", true, false, true, true, false, false)]
    [InlineData("PendingReview", true, false, false, false, false, false)]
    [InlineData("PendingReview", false, true, false, false, true, true)]
    [InlineData("Published", false, true, true, false, false, false)]   // editors/admins may edit what is published
    [InlineData("Published", true, false, false, false, false, false)]   // an author may not, even their own
    public void RowActions_FollowTheWorkflowRules(string status, bool owner, bool privileged, bool edit, bool submit, bool publish, bool reject)
    {
        var actions = CmsRowActions.For(status, owner, privileged);
        Assert.Equal(status == "Published", actions.CanViewPublic);
        Assert.Equal(edit, actions.CanEdit);
        Assert.Equal(submit, actions.CanSubmit);
        Assert.Equal(publish, actions.CanPublish);
        Assert.Equal(reject, actions.CanReject);
    }

    [Fact]
    public async Task Author_DoesNotSeeReviewActionsOrAdminAreas_EditorDoes()
    {
        var queued = await CreateArticleAsync("author@vnews.local", "Bài chờ duyệt cho phân quyền", submit: true);

        var authorHtml = await (await (await _factory.LoginCmsAsync("author@vnews.local")).GetAsync("/cms")).Content.ReadAsStringAsync();
        Assert.DoesNotContain($"/CmsArticles/Reject/{queued.Id}", authorHtml);
        Assert.DoesNotContain($"/CmsArticles/Publish/{queued.Id}", authorHtml);
        Assert.DoesNotContain("/cms/CmsUsers", authorHtml);
        Assert.DoesNotContain("/cms/CmsAudit", authorHtml);

        var editorHtml = await (await (await _factory.LoginCmsAsync("editor@vnews.local")).GetAsync("/cms?status=PendingReview")).Content.ReadAsStringAsync();
        Assert.Contains($"/CmsArticles/Reject/{queued.Id}", editorHtml);
        Assert.Contains($"/CmsArticles/Publish/{queued.Id}", editorHtml);
        Assert.DoesNotContain("/cms/CmsUsers", editorHtml);

        var adminHtml = await (await (await _factory.LoginCmsAsync("admin@vnews.local")).GetAsync("/cms")).Content.ReadAsStringAsync();
        Assert.Contains("/cms/CmsUsers", adminHtml);
        Assert.Contains("/cms/CmsAudit", adminHtml);
    }

    [Fact]
    public async Task Author_CannotSeeAnotherAuthorsArticleInTheList_OrOpenIt()
    {
        var other = await _factory.CreateUserAsync(Roles.Author, "Tác giả khác");
        var theirs = await CreateArticleAsync(other.Email, "Bài riêng của tác giả khác", submit: false);

        var client = await _factory.LoginCmsAsync("author@vnews.local");
        Assert.DoesNotContain("Bài riêng của tác giả khác", await client.GetStringAsync("/cms?q=Bài+riêng"));
        var open = await client.GetAsync($"/cms/CmsArticles/Edit/{theirs.Id}");
        Assert.Equal(HttpStatusCode.Redirect, open.StatusCode); // forbidden -> access denied page
    }

    [Fact]
    public async Task InvalidFilters_AreIgnored_NotErrors()
    {
        var client = await _factory.LoginCmsAsync("editor@vnews.local");
        foreach (var url in new[] { "/cms?status=Hacked", "/cms?page=-4", "/cms?page=9999", "/cms?q=" + new string('x', 400) })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
        }
    }

    // ---------- admin-only pages ----------

    [Theory]
    [InlineData("/cms/CmsOverview")]
    [InlineData("/cms/CmsAudit")]
    [InlineData("/cms/CmsUsers")]
    public async Task AdminPages_AreForbiddenForStaff_ChallengedForAnonymous_AndOpenForAdmin(string path)
    {
        Assert.Equal(HttpStatusCode.Redirect, (await _factory.NewClient().GetAsync(path)).StatusCode);
        foreach (var email in new[] { "author@vnews.local", "editor@vnews.local" })
        {
            var denied = await (await _factory.LoginCmsAsync(email)).GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Contains("/tai-khoan/khong-co-quyen", denied.Headers.Location!.ToString());
        }
        Assert.Equal(HttpStatusCode.OK, (await (await _factory.LoginCmsAsync("admin@vnews.local")).GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Admin_CannotChangeOwnRole_ButCanChangeAnotherUsersRole()
    {
        var admin = await _factory.LoginCmsAsync("admin@vnews.local");
        var target = await _factory.CreateUserAsync(Roles.Reader);
        string adminId;
        using (var scope = _factory.Services.CreateScope())
            adminId = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("admin@vnews.local"))!.Id;

        await admin.PostFormAsync("/cms/CmsUsers/SetRole", "/cms/CmsUsers", new() { ["userId"] = adminId, ["role"] = Roles.Reader });
        await admin.PostFormAsync("/cms/CmsUsers/SetRole", "/cms/CmsUsers", new() { ["userId"] = target.Id, ["role"] = Roles.Author });

        using var check = _factory.Services.CreateScope();
        var users = check.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Contains(Roles.Admin, await users.GetRolesAsync((await users.FindByIdAsync(adminId))!));
        Assert.Equal(new[] { Roles.Author }, await users.GetRolesAsync((await users.FindByIdAsync(target.Id))!));
    }

    [Fact]
    public async Task UsersList_FiltersByRoleAndSearch()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        await _factory.CreateUserAsync(Roles.Editor, $"Biên tập {marker}");
        var admin = await _factory.LoginCmsAsync("admin@vnews.local");

        var byName = await admin.GetStringAsync($"/cms/CmsUsers?q={marker}");
        Assert.Contains(marker, WebUtility.HtmlDecode(byName));
        var wrongRole = await admin.GetStringAsync($"/cms/CmsUsers?q={marker}&role={Roles.Admin}");
        Assert.Contains("Không tìm thấy tài khoản phù hợp", WebUtility.HtmlDecode(wrongRole));
    }

    // ---------- account pages ----------

    [Fact]
    public async Task Profile_ShowsRealRole_AndUpdatesTheDisplayName()
    {
        var reader = await _factory.CreateUserAsync(Roles.Reader, "Độc giả ban đầu");
        var client = await _factory.LoginCmsAsync(reader.Email, "TestPassword123!");

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/tai-khoan/ho-so"));
        Assert.Contains("Độc giả", html);
        Assert.DoesNotContain("/cms/CmsArticles", html); // no newsroom entry for readers

        var update = await client.PostFormAsync("/tai-khoan/ho-so", "/tai-khoan/ho-so", new() { ["DisplayName"] = "Tên mới của tôi" });
        Assert.Equal(HttpStatusCode.Redirect, update.StatusCode);
        Assert.Contains("Tên mới của tôi", WebUtility.HtmlDecode(await client.GetStringAsync("/tai-khoan/ho-so")));

        var invalid = await client.PostFormAsync("/tai-khoan/ho-so", "/tai-khoan/ho-so", new() { ["DisplayName"] = "A" });
        Assert.Equal(HttpStatusCode.Redirect, invalid.StatusCode);
        Assert.Contains("Tên mới của tôi", WebUtility.HtmlDecode(await client.GetStringAsync("/tai-khoan/ho-so"))); // unchanged
    }

    [Fact]
    public async Task StaffProfile_ShowsTheirRoleAndTheNewsroomEntry()
    {
        var html = WebUtility.HtmlDecode(await (await _factory.LoginCmsAsync("editor@vnews.local")).GetStringAsync("/tai-khoan/ho-so"));
        Assert.Contains("Biên tập viên", html);
        Assert.Contains("Vào tòa soạn", html);
        Assert.DoesNotContain("HỒ SƠ ĐỘC GIẢ", html);
    }

    [Fact]
    public async Task SavedArticles_AreListedForTheOwner_CanBeRemoved_AndAreNotSharedBetweenUsers()
    {
        var reader = await _factory.CreateUserAsync(Roles.Reader);
        var stranger = await _factory.CreateUserAsync(Roles.Reader);
        int articleId;
        string title;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
            var article = await db.Articles.AsNoTracking().Where(a => a.Status == ArticleStatus.Published).OrderBy(a => a.Id).FirstAsync();
            articleId = article.Id;
            title = article.Title;
            db.SavedArticles.Add(new SavedArticle { UserId = reader.Id, ArticleId = articleId, SavedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var client = await _factory.LoginCmsAsync(reader.Email, "TestPassword123!");
        Assert.Contains(title, await client.GetDecodedAsync("/tai-khoan/bai-da-luu"));

        var other = await _factory.LoginCmsAsync(stranger.Email, "TestPassword123!");
        Assert.DoesNotContain(title, await other.GetDecodedAsync("/tai-khoan/bai-da-luu"));
        await other.PostFormAsync($"/tai-khoan/bai-da-luu/bo-luu/{articleId}", "/tai-khoan/bai-da-luu", new()); // must not remove someone else's
        Assert.Contains(title, await client.GetDecodedAsync("/tai-khoan/bai-da-luu"));

        // the owner's page has the token; the stranger has no bookmark so no unsave form there.
        using (var scope = _factory.Services.CreateScope())
        {
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().SavedArticles.CountAsync(s => s.ArticleId == articleId && s.UserId == reader.Id));
        }

        var remove = await client.PostFormAsync($"/tai-khoan/bai-da-luu/bo-luu/{articleId}", "/tai-khoan/bai-da-luu", new());
        Assert.Equal(HttpStatusCode.Redirect, remove.StatusCode);
        Assert.DoesNotContain(title, await client.GetDecodedAsync("/tai-khoan/bai-da-luu"));
    }

    [Fact]
    public async Task SavedArticles_RequiresSignIn()
    {
        var response = await _factory.NewClient().GetAsync("/tai-khoan/bai-da-luu");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/tai-khoan/dang-nhap", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task TopBar_GreetsWithTheDisplayName_NotTheEmail()
    {
        var reader = await _factory.CreateUserAsync(Roles.Reader, "Nguyễn Văn Thử");
        var html = WebUtility.HtmlDecode(await (await _factory.LoginCmsAsync(reader.Email, "TestPassword123!")).GetStringAsync("/tai-khoan/ho-so"));
        Assert.Contains("<span class=\"app-user__name\">Nguyễn Văn Thử</span>", html);
    }

    [Fact]
    public async Task LoginPage_NoLongerShowsTheReaderOnlyLabelOrTheStaffLink_ButKeepsTheDemoCards()
    {
        var html = WebUtility.HtmlDecode(await _factory.NewClient().GetStringAsync("/tai-khoan/dang-nhap"));
        Assert.Contains("Chào mừng trở lại", html);
        Assert.DoesNotContain("TÀI KHOẢN ĐỘC GIẢ", html);
        Assert.DoesNotContain("Nhân viên tòa soạn", html);
        Assert.DoesNotContain("vào CMS", html);
    }

    [Fact]
    public async Task ArticleList_ShowsCoverThumbnailOrMarker_AndEditPageShowsTheCurrentCover()
    {
        int withCover, withoutCover;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var author = (await users.FindByEmailAsync("author@vnews.local"))!;
            var svc = scope.ServiceProvider.GetRequiredService<IArticleCommandService>();
            var category = await db.Categories.Select(c => c.Id).FirstAsync();
            withCover = await svc.CreateDraftAsync(new ArticleSaveRequest { Title = "Bài có ảnh bìa thử", Summary = "Tóm tắt", Body = "<p>Nội dung đủ dài để hợp lệ.</p>", CategoryId = category, CoverImageUrl = "/images/demo/cong-nghe.jpg" }, author.Id, author.DisplayName);
            withoutCover = await svc.CreateDraftAsync(new ArticleSaveRequest { Title = "Bài chưa có ảnh bìa thử", Summary = "Tóm tắt", Body = "<p>Nội dung đủ dài để hợp lệ.</p>", CategoryId = category }, author.Id, author.DisplayName);
        }
        var client = await _factory.LoginCmsAsync("author@vnews.local");

        var list = WebUtility.HtmlDecode(await client.GetStringAsync("/cms?q=ảnh+bìa+thử"));
        Assert.Contains("class=\"article-cell__thumb\" src=\"/images/demo/cong-nghe.jpg\"", list);
        Assert.Contains("Chưa có ảnh bìa", list);

        var edit = WebUtility.HtmlDecode(await client.GetStringAsync($"/cms/CmsArticles/Edit/{withCover}"));
        Assert.Contains("data-cover-img src=\"/images/demo/cong-nghe.jpg\"", edit);
        Assert.Contains("Ảnh hiện tại", edit);
        Assert.Contains("Thay ảnh", edit);

        var none = WebUtility.HtmlDecode(await client.GetStringAsync($"/cms/CmsArticles/Edit/{withoutCover}"));
        Assert.Contains("Chưa có ảnh bìa", none);
        Assert.Contains("Chọn ảnh", none);
    }

    [Fact]
    public async Task AccountPages_PostTheirFormsToTheRightControllers_IncludingSignOut()
    {
        var reader = await _factory.CreateUserAsync(Roles.Reader);
        var client = await _factory.LoginCmsAsync(reader.Email!, TestUsers.Password);

        var profile = await client.GetDecodedAsync("/tai-khoan/ho-so");
        Assert.Contains("action=\"/tai-khoan/dang-xuat\"", profile);   // sign-out lives in AccountController, the page in ReaderAccountController
        Assert.Contains("action=\"/tai-khoan/ho-so\"", profile);

        var logout = await client.PostFormAsync("/tai-khoan/dang-xuat", "/tai-khoan/ho-so", new());
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/tai-khoan/ho-so")).StatusCode); // signed out: the page asks for sign-in again
    }
}
