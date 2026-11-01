using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Domain.Identity;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;
using VNews.Web.Services;
using Microsoft.AspNetCore.Identity;

namespace VNews.Tests.Integration;

/// <summary>Comments: who may post, validation, replies, moderation and withdrawal. Real HTTP requests, isolated database.</summary>
public sealed class CommentTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public CommentTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private async Task<(int Id, string Slug)> PublishedArticleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var a = await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Articles.AsNoTracking()
            .Where(x => x.Status == ArticleStatus.Published).OrderBy(x => x.Id).Select(x => new { x.Id, x.Slug }).FirstAsync();
        return (a.Id, a.Slug);
    }

    private async Task<(string Email, string Id)> NewUserAsync(string role, string name = "Bạn đọc thử")
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = name, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "TestPassword123!")).Succeeded);
        await users.AddToRoleAsync(user, role);
        return (email, user.Id);
    }

    private async Task<HttpClient> ReaderAsync(string name = "Bạn đọc thử")
    {
        var user = await NewUserAsync(Roles.Reader, name);
        return await _factory.LoginCmsAsync(user.Email, "TestPassword123!");
    }

    private static async Task<string> PageAsync(HttpClient client, string path) => WebUtility.HtmlDecode(await client.GetStringAsync(path));

    private async Task<int> CommentCountAsync(int articleId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Comments.CountAsync(c => c.ArticleId == articleId);
    }

    private Task<HttpResponseMessage> PostCommentAsync(HttpClient client, string slug, string body, int? parentId = null, string? tokenPage = null)
    {
        var fields = new Dictionary<string, string> { ["body"] = body };
        if (parentId is int p) fields["parentId"] = p.ToString();
        return client.PostFormAsync($"/tin/{slug}/binh-luan", tokenPage ?? $"/tin/{slug}", fields);
    }

    // ---------- posting ----------

    [Fact]
    public async Task Anonymous_SeesTheListAndALoginPrompt_ButCannotPost()
    {
        var article = await PublishedArticleAsync();
        var anonymous = _factory.NewClient();

        var html = await PageAsync(anonymous, $"/tin/{article.Slug}");
        Assert.Contains("id=\"binh-luan\"", html);
        Assert.Contains("Đăng nhập để tham gia bình luận", html);
        Assert.DoesNotContain("id=\"comment-form\"", html);

        var before = await CommentCountAsync(article.Id);
        var response = await anonymous.PostAsync($"/tin/{article.Slug}/binh-luan", new FormUrlEncodedContent(new Dictionary<string, string> { ["body"] = "Bình luận ẩn danh" }));
        Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.BadRequest);
        Assert.Equal(before, await CommentCountAsync(article.Id));
    }

    [Fact]
    public async Task Reader_PostsAComment_ItIsShownEncoded_AndTheCountGrows()
    {
        var article = await PublishedArticleAsync();
        var reader = await ReaderAsync("Người bình luận");
        var marker = Guid.NewGuid().ToString("N")[..8];

        var response = await PostCommentAsync(reader, article.Slug, $"Bài hay {marker} <script>alert(1)</script> <b>đậm</b>");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/tin/{article.Slug}#comment-", response.Headers.Location!.ToString());

        var raw = await reader.GetStringAsync($"/tin/{article.Slug}");
        var html = WebUtility.HtmlDecode(raw);
        Assert.Contains("Người bình luận", html);
        Assert.Contains(marker, html);
        Assert.DoesNotContain("<script>alert(1)</script>", raw);   // never rendered as markup
        Assert.DoesNotContain("<b>đậm</b>", raw);
        Assert.Contains("&lt;script&gt;", raw);
        Assert.Contains("Đã đăng bình luận của bạn", html);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    public async Task EmptyOrTinyComment_IsRefused_NothingStored_AndTheMessageIsShown(string body)
    {
        var article = await PublishedArticleAsync();
        var reader = await ReaderAsync();
        var before = await CommentCountAsync(article.Id);

        var response = await PostCommentAsync(reader, article.Slug, body);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(before, await CommentCountAsync(article.Id));
        Assert.Contains("Vui lòng nhập nội dung bình luận", await PageAsync(reader, $"/tin/{article.Slug}"));
    }

    [Fact]
    public async Task TooLongComment_IsRefused_AndTheTypedTextIsKept()
    {
        var article = await PublishedArticleAsync();
        var reader = await ReaderAsync();
        var before = await CommentCountAsync(article.Id);
        var longText = "Rất dài " + new string('x', ICommentService.MaxLength + 50);

        await PostCommentAsync(reader, article.Slug, longText);

        Assert.Equal(before, await CommentCountAsync(article.Id));
        var html = await PageAsync(reader, $"/tin/{article.Slug}");
        Assert.Contains("Bình luận tối đa", html);
        Assert.Contains("Rất dài", html); // the draft survives the refusal
    }

    [Fact]
    public async Task CommentOnADraftOrMissingArticle_IsNotFound_AndNothingIsStored()
    {
        int draftId;
        string draftSlug;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
            var article = new Article { Title = "Nháp riêng", Slug = "nhap-rieng-" + Guid.NewGuid().ToString("N")[..6], Summary = "x", Body = "<p>x</p>", AuthorName = "A", Status = ArticleStatus.Draft, CategoryId = await db.Categories.Select(c => c.Id).FirstAsync() };
            db.Articles.Add(article);
            await db.SaveChangesAsync();
            draftId = article.Id; draftSlug = article.Slug;
        }
        var reader = await ReaderAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await PostCommentAsync(reader, draftSlug, "Bình luận vào bản nháp", tokenPage: "/tai-khoan/ho-so")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostCommentAsync(reader, "khong-co-bai-nay", "Bình luận vào bài không có", tokenPage: "/tai-khoan/ho-so")).StatusCode);
        Assert.Equal(0, await CommentCountAsync(draftId));
    }

    [Fact]
    public async Task PostWithoutAntiforgeryToken_IsRefused()
    {
        var article = await PublishedArticleAsync();
        var reader = await ReaderAsync();
        var before = await CommentCountAsync(article.Id);

        var response = await reader.PostAsync($"/tin/{article.Slug}/binh-luan", new FormUrlEncodedContent(new Dictionary<string, string> { ["body"] = "Không có token" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await CommentCountAsync(article.Id));
    }

    [Fact]
    public async Task SixthCommentInTwoMinutes_IsRateLimited_AndAnIdenticalRepeatIsRefused()
    {
        var article = await PublishedArticleAsync();
        var reader = await ReaderAsync();
        var before = await CommentCountAsync(article.Id);

        for (var i = 0; i < 5; i++) await PostCommentAsync(reader, article.Slug, $"Bình luận số {i} {Guid.NewGuid():N}");
        Assert.Equal(before + 5, await CommentCountAsync(article.Id));

        await PostCommentAsync(reader, article.Slug, "Bình luận thứ sáu quá nhanh");
        Assert.Equal(before + 5, await CommentCountAsync(article.Id));
        Assert.Contains("hơi nhanh", await PageAsync(reader, $"/tin/{article.Slug}"));

        var other = await ReaderAsync();
        var text = "Một nội dung " + Guid.NewGuid().ToString("N");
        await PostCommentAsync(other, article.Slug, text);
        await PostCommentAsync(other, article.Slug, text);
        using var scope = _factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Comments.CountAsync(c => c.Body == text));
    }

    // ---------- replies ----------

    [Fact]
    public async Task Reply_IsNestedUnderItsParent_AndAReplyToAReplyIsRefused()
    {
        var article = await PublishedArticleAsync();
        var reader = await ReaderAsync("Người trả lời");
        var marker = Guid.NewGuid().ToString("N")[..8];
        await PostCommentAsync(reader, article.Slug, $"Gốc {marker}");
        int parentId;
        using (var scope = _factory.Services.CreateScope())
            parentId = await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Comments.Where(c => c.Body == $"Gốc {marker}").Select(c => c.Id).SingleAsync();

        await PostCommentAsync(reader, article.Slug, $"Trả lời {marker}", parentId);
        int replyId;
        using (var scope = _factory.Services.CreateScope())
            replyId = await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Comments.Where(c => c.ParentId == parentId).Select(c => c.Id).SingleAsync();

        var html = await PageAsync(reader, $"/tin/{article.Slug}");
        var parentAt = html.IndexOf($"id=\"comment-{parentId}\"", StringComparison.Ordinal);
        var replyAt = html.IndexOf($"id=\"comment-{replyId}\"", StringComparison.Ordinal);
        Assert.True(parentAt >= 0 && replyAt > parentAt);
        Assert.Contains("comment-list--replies", html);

        var before = await CommentCountAsync(article.Id);
        await PostCommentAsync(reader, article.Slug, $"Trả lời của trả lời {marker}", replyId); // second level: not allowed
        Assert.Equal(before, await CommentCountAsync(article.Id));

        // a parent from ANOTHER article is not accepted either
        using var scope2 = _factory.Services.CreateScope();
        var otherSlug = await scope2.ServiceProvider.GetRequiredService<VNewsDbContext>().Articles.Where(a => a.Status == ArticleStatus.Published && a.Id != article.Id).Select(a => a.Slug).FirstAsync();
        await PostCommentAsync(reader, otherSlug, $"Sai bài {marker}", parentId);
        Assert.Equal(before, await CommentCountAsync(article.Id));
        using var scope3 = _factory.Services.CreateScope();
        Assert.False(await scope3.ServiceProvider.GetRequiredService<VNewsDbContext>().Comments.AnyAsync(c => c.Body == $"Sai bài {marker}"));
    }

    // ---------- moderation and withdrawal ----------

    [Fact]
    public async Task Reader_CannotHideComments_Editor_Can_AndTheActionIsAudited()
    {
        var article = await PublishedArticleAsync();
        var author = await ReaderAsync("Tác giả bình luận");
        var marker = Guid.NewGuid().ToString("N")[..8];
        await PostCommentAsync(author, article.Slug, $"Cần gỡ {marker}");
        int id;
        using (var scope = _factory.Services.CreateScope())
            id = await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Comments.Where(c => c.Body == $"Cần gỡ {marker}").Select(c => c.Id).SingleAsync();

        var stranger = await ReaderAsync();
        var denied = await stranger.PostFormAsync($"/binh-luan/{id}/go", $"/tin/{article.Slug}", new());
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("khong-co-quyen", denied.Headers.Location!.ToString());
        Assert.Contains($"Cần gỡ {marker}", await PageAsync(author, $"/tin/{article.Slug}"));

        var editor = await _factory.LoginCmsAsync("editor@vnews.local");
        var ok = await editor.PostFormAsync($"/binh-luan/{id}/go", $"/tin/{article.Slug}", new());
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);

        var html = await PageAsync(author, $"/tin/{article.Slug}");
        Assert.DoesNotContain($"Cần gỡ {marker}", html);
        using var check = _factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<VNewsDbContext>();
        Assert.Equal(CommentStatus.Hidden, (await db.Comments.AsNoTracking().FirstAsync(c => c.Id == id)).Status);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "HideComment" && a.EntityId == id.ToString()));
    }

    [Fact]
    public async Task Author_CanWithdrawOwnComment_ButNotSomeoneElses()
    {
        var article = await PublishedArticleAsync();
        var mine = await ReaderAsync();
        var theirs = await ReaderAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        await PostCommentAsync(theirs, article.Slug, $"Của người khác {marker}");
        await PostCommentAsync(mine, article.Slug, $"Của tôi {marker}");
        int theirId, myId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
            theirId = await db.Comments.Where(c => c.Body == $"Của người khác {marker}").Select(c => c.Id).SingleAsync();
            myId = await db.Comments.Where(c => c.Body == $"Của tôi {marker}").Select(c => c.Id).SingleAsync();
        }

        var forbidden = await mine.PostFormAsync($"/binh-luan/{theirId}/xoa", $"/tin/{article.Slug}", new());
        Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
        Assert.Contains("khong-co-quyen", forbidden.Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.Redirect, (await mine.PostFormAsync($"/binh-luan/{myId}/xoa", $"/tin/{article.Slug}", new())).StatusCode);

        var html = await PageAsync(mine, $"/tin/{article.Slug}");
        Assert.Contains($"Của người khác {marker}", html);
        Assert.DoesNotContain($"Của tôi {marker}", html);
    }

    [Fact]
    public async Task RemovedParentWithLiveReplies_ShowsAPlaceholder_AndKeepsTheReplies()
    {
        var article = await PublishedArticleAsync();
        var reader = await ReaderAsync();
        var replier = await ReaderAsync("Người còn lại");
        var marker = Guid.NewGuid().ToString("N")[..8];
        await PostCommentAsync(reader, article.Slug, $"Sắp xóa {marker}");
        int parentId;
        using (var scope = _factory.Services.CreateScope())
            parentId = await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Comments.Where(c => c.Body == $"Sắp xóa {marker}").Select(c => c.Id).SingleAsync();
        await PostCommentAsync(replier, article.Slug, $"Còn lại {marker}", parentId);

        await reader.PostFormAsync($"/binh-luan/{parentId}/xoa", $"/tin/{article.Slug}", new());

        var html = await PageAsync(replier, $"/tin/{article.Slug}");
        Assert.DoesNotContain($"Sắp xóa {marker}", html);
        Assert.Contains("Bình luận đã được tác giả xóa", html);
        Assert.Contains($"Còn lại {marker}", html);
    }
}
