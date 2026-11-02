using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Application.Common;
using VNews.Application.DTOs;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Domain.Identity;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>
/// The CMS delete / restore operations on their own (no HTTP): soft delete of articles, category deletion rules, account locking and
/// comment moderation. Nothing is ever removed permanently except an empty category. Isolated database per fixture.
/// </summary>
public sealed class CmsCrudTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public CmsCrudTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private static T Service<T>(IServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    private async Task<Article> NewArticleAsync(ArticleStatus status, string? authorUserId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Service<VNewsDbContext>(scope);
        var article = new Article
        {
            Title = "Bài thử xóa mềm " + Guid.NewGuid().ToString("N")[..6], Slug = "bai-thu-xoa-mem-" + Guid.NewGuid().ToString("N")[..8],
            Summary = "Tóm tắt", Body = "<p>Nội dung bài thử.</p>", AuthorName = "Tác giả thử", AuthorUserId = authorUserId, Status = status,
            PublishedAt = status == ArticleStatus.Published ? new DateTime(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc) : null,
            CategoryId = await db.Categories.OrderBy(c => c.Id).Select(c => c.Id).FirstAsync(),
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return article;
    }

    private async Task<Article> ReloadAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await Service<VNewsDbContext>(scope).Articles.AsNoTracking().SingleAsync(a => a.Id == id);
    }

    // ---------- articles: trash and restore ----------

    [Fact]
    public async Task Trash_Published_KeepsSlugAndPublishedAt_LeavesThePublicSite_AndRestoreBringsItBackUnchanged()
    {
        var article = await NewArticleAsync(ArticleStatus.Published);
        using var scope = _factory.Services.CreateScope();
        var trash = Service<IArticleTrashService>(scope);

        await trash.MoveToTrashAsync(article.Id, null, "editor-1", "Biên tập", canManageAll: true);
        var trashed = await ReloadAsync(article.Id);
        Assert.Equal(ArticleStatus.Trashed, trashed.Status);
        Assert.Equal(ArticleStatus.Published, trashed.StatusBeforeDeletion);
        Assert.Equal(article.Slug, trashed.Slug);
        Assert.Equal(article.PublishedAt, trashed.PublishedAt);

        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/tin/{article.Slug}")).StatusCode);

        await trash.RestoreAsync(article.Id, null, "editor-1", "Biên tập", canManageAll: true);
        var restored = await ReloadAsync(article.Id);
        Assert.Equal(ArticleStatus.Published, restored.Status);
        Assert.Equal(article.Slug, restored.Slug);
        Assert.Equal(article.PublishedAt, restored.PublishedAt);
        Assert.Null(restored.DeletedAt);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/tin/{article.Slug}")).StatusCode);
    }

    [Fact]
    public async Task Trash_AuthorMayDiscardOnlyOwnDraft_NotPublishedNorOthers()
    {
        var own = await NewArticleAsync(ArticleStatus.Draft, "author-A");
        var otherDraft = await NewArticleAsync(ArticleStatus.Draft, "author-B");
        var published = await NewArticleAsync(ArticleStatus.Published, "author-A");
        using var scope = _factory.Services.CreateScope();
        var trash = Service<IArticleTrashService>(scope);

        await Assert.ThrowsAsync<ArticleForbiddenException>(() => trash.MoveToTrashAsync(otherDraft.Id, null, "author-A", "A", canManageAll: false));
        await Assert.ThrowsAsync<ArticleForbiddenException>(() => trash.MoveToTrashAsync(published.Id, null, "author-A", "A", canManageAll: false));
        await trash.MoveToTrashAsync(own.Id, null, "author-A", "A", canManageAll: false);
        Assert.Equal(ArticleStatus.Trashed, (await ReloadAsync(own.Id)).Status);

        await Assert.ThrowsAsync<ArticleWorkflowException>(() => trash.MoveToTrashAsync(own.Id, null, "author-A", "A", canManageAll: false)); // already trashed
        await Assert.ThrowsAsync<ArticleForbiddenException>(() => trash.RestoreAsync(own.Id, null, "author-B", "B", canManageAll: false));
        await trash.RestoreAsync(own.Id, null, "author-A", "A", canManageAll: false);
        Assert.Equal(ArticleStatus.Draft, (await ReloadAsync(own.Id)).Status);
    }

    [Fact]
    public async Task Trash_StaleRowVersion_IsAConflict_AndAnAuditEntryIsWritten()
    {
        var article = await NewArticleAsync(ArticleStatus.Draft, "author-A");
        using (var bump = _factory.Services.CreateScope())
        {
            var db = Service<VNewsDbContext>(bump);
            (await db.Articles.SingleAsync(a => a.Id == article.Id)).Title += " (đã sửa)";
            await db.SaveChangesAsync();
        }

        using var scope = _factory.Services.CreateScope();
        var trash = Service<IArticleTrashService>(scope);
        await Assert.ThrowsAsync<ArticleConcurrencyException>(() => trash.MoveToTrashAsync(article.Id, article.RowVersion, "editor-1", "Biên tập", canManageAll: true));
        Assert.Equal(ArticleStatus.Draft, (await ReloadAsync(article.Id)).Status);

        using var retry = _factory.Services.CreateScope(); // a new request: a new context, so the failed attempt is not still tracked
        await Service<IArticleTrashService>(retry).MoveToTrashAsync(article.Id, null, "editor-1", "Biên tập", canManageAll: true);
        var db2 = Service<VNewsDbContext>(retry);
        Assert.True(await db2.AuditLogs.AnyAsync(l => l.Action == "Trash" && l.EntityId == article.Id.ToString()));
    }

    [Fact]
    public async Task Trash_TrashedArticleCannotBeEdited_AndIsHiddenFromTheDefaultCmsList()
    {
        var article = await NewArticleAsync(ArticleStatus.Draft, "author-A");
        using var scope = _factory.Services.CreateScope();
        await Service<IArticleTrashService>(scope).MoveToTrashAsync(article.Id, null, "author-A", "A", canManageAll: false);

        var request = new ArticleSaveRequest { Title = "Tiêu đề mới hợp lệ", Summary = "Tóm tắt hợp lệ", Body = "<p>Nội dung đủ dài để hợp lệ.</p>", CategoryId = article.CategoryId };
        var rowVersion = (await ReloadAsync(article.Id)).RowVersion;
        await Assert.ThrowsAsync<ArticleWorkflowException>(() => Service<IArticleCommandService>(scope).UpdateAsync(article.Id, request, rowVersion, "author-A", canManageAll: false));

        var query = Service<IArticleCmsQueryService>(scope);
        Assert.DoesNotContain((await query.ListForCmsAsync(null, null, 1, 100)).Items, i => i.Id == article.Id);
        Assert.Contains((await query.ListForCmsAsync("Trashed", null, 1, 100)).Items, i => i.Id == article.Id);
        Assert.True((await query.GetCmsStatsAsync(null)).Trashed >= 1);
    }

    // ---------- categories ----------

    [Fact]
    public async Task DeleteCategory_OnlyWhenEmpty_NoArticlesAndNoChildren()
    {
        using var scope = _factory.Services.CreateScope();
        var categories = Service<ICategoryCommandService>(scope);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var parentId = await categories.CreateAsync("Cha " + tag, "cha-" + tag, null, 90);
        var childId = await categories.CreateAsync("Con " + tag, "con-" + tag, null, 91, parentId);

        await Assert.ThrowsAsync<ArticleWorkflowException>(() => categories.DeleteAsync(parentId, "admin-1", "Quản trị")); // has a child
        await categories.DeleteAsync(childId, "admin-1", "Quản trị");
        await categories.DeleteAsync(parentId, "admin-1", "Quản trị");
        await Assert.ThrowsAsync<EntityNotFoundException>(() => categories.DeleteAsync(parentId, "admin-1", "Quản trị"));

        var withArticle = (await NewArticleAsync(ArticleStatus.Draft)).CategoryId;
        await Assert.ThrowsAsync<ArticleWorkflowException>(() => categories.DeleteAsync(withArticle, "admin-1", "Quản trị"));
    }

    // ---------- accounts: lock / unlock ----------

    [Fact]
    public async Task LockUser_BlocksSignIn_NeverLocksSelfOrTheLastActiveAdmin_AndUnlockRestoresAccess()
    {
        var admin = await _factory.CreateUserAsync(Roles.Admin);
        var reader = await _factory.CreateUserAsync(Roles.Reader);
        using var scope = _factory.Services.CreateScope();
        var users = Service<IUserAdministrationService>(scope);

        Assert.Equal(SetLockOutcome.CannotLockSelf, (await users.SetLockedAsync(admin.Id, "A", admin.Id, true)).Outcome);
        Assert.Equal(SetLockOutcome.UserNotFound, (await users.SetLockedAsync(admin.Id, "A", "nobody", true)).Outcome);

        Assert.Equal(SetLockOutcome.Changed, (await users.SetLockedAsync(admin.Id, "A", reader.Id, true)).Outcome);
        Assert.True((await users.ListAsync(reader.Email, null, 1, 10)).Items.Single().IsLocked);
        using (var client = _factory.CreateClient())
        {
            var login = await client.PostAsync("/tai-khoan/dang-nhap", new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = reader.Email, ["Password"] = TestUsers.Password }));
            Assert.NotEqual(HttpStatusCode.Redirect, login.StatusCode);
        }

        Assert.Equal(SetLockOutcome.Changed, (await users.SetLockedAsync(admin.Id, "A", reader.Id, false)).Outcome);
        Assert.False((await users.ListAsync(reader.Email, null, 1, 10)).Items.Single().IsLocked);
    }

    // ---------- comments: moderation ----------

    [Fact]
    public async Task Comments_HideThenRestore_AreAudited_AndAuthorDeletedCommentsStayDeleted()
    {
        var article = await NewArticleAsync(ArticleStatus.Published);
        using var scope = _factory.Services.CreateScope();
        var db = Service<VNewsDbContext>(scope);
        var reader1 = await _factory.CreateUserAsync(VNews.Domain.Identity.Roles.Reader);
        var reader2 = await _factory.CreateUserAsync(VNews.Domain.Identity.Roles.Reader);
        var visible = new Comment { ArticleId = article.Id, UserId = reader1.Id, AuthorName = "Bạn đọc", Body = "Bình luận thử để kiểm duyệt" };
        var deleted = new Comment { ArticleId = article.Id, UserId = reader2.Id, AuthorName = "Bạn đọc 2", Body = "Bình luận tác giả đã xóa", Status = CommentStatus.Deleted };
        db.Comments.AddRange(visible, deleted);
        await db.SaveChangesAsync();

        var comments = Service<ICommentService>(scope);
        var moderation = Service<ICommentModerationService>(scope);

        Assert.True((await comments.HideAsync(visible.Id, "editor-1", "Biên tập")).Success);
        Assert.Contains((await moderation.ListAsync("Hidden", null, 1, 50)).Items, i => i.Id == visible.Id);
        Assert.True((await moderation.RestoreAsync(visible.Id, "editor-1", "Biên tập")).Success);
        Assert.Contains((await moderation.ListAsync("Visible", "kiểm duyệt", 1, 50)).Items, i => i.Id == visible.Id);

        var refused = await moderation.RestoreAsync(deleted.Id, "editor-1", "Biên tập");
        Assert.False(refused.Success);
        Assert.Equal(CommentFailure.Forbidden, refused.Failure);
        Assert.Equal(CommentStatus.Deleted, (await db.Comments.AsNoTracking().SingleAsync(c => c.Id == deleted.Id)).Status);
        Assert.True(await db.AuditLogs.AnyAsync(l => l.Action == "RestoreComment" && l.EntityId == visible.Id.ToString()));
    }

    // ---------- HTTP: who may open the new pages ----------

    [Fact]
    public async Task ModerationPage_OpensForEditor_NotForAuthorReaderOrAnonymous()
    {
        using var editor = await _factory.LoginCmsAsync("editor@vnews.local");
        Assert.Equal(HttpStatusCode.OK, (await editor.GetAsync("/cms/CmsComments")).StatusCode);

        using var author = await _factory.LoginCmsAsync("author@vnews.local");
        Assert.NotEqual(HttpStatusCode.OK, (await author.GetAsync("/cms/CmsComments")).StatusCode);

        using var reader = await _factory.LoginNewReaderAsync();
        Assert.NotEqual(HttpStatusCode.OK, (await reader.GetAsync("/cms/CmsComments")).StatusCode);

        using var anonymous = _factory.CreateClient(new() { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/cms/CmsComments")).StatusCode);
    }
}
