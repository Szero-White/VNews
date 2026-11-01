using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VNews.Application.Common;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>
/// Real SQL Server (isolated LocalDB). The audit-log write is forced to fail to prove that the
/// article change made in the same operation is rolled back, not left half-applied.
/// </summary>
public class ArticleTransactionIntegrationTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public ArticleTransactionIntegrationTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private static ArticleSaveRequest Request(int categoryId, string? title = null) => new()
    {
        Title = title ?? $"Tx test {Guid.NewGuid():N}",
        Summary = "Tom tat bai viet kiem thu giao dich.",
        Body = "<p>Noi dung kiem thu giao dich co du dai.</p>",
        CategoryId = categoryId
    };

    private async Task<T> InScope<T>(Func<IArticleCommandService, VNewsDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(
            scope.ServiceProvider.GetRequiredService<IArticleCommandService>(),
            scope.ServiceProvider.GetRequiredService<VNewsDbContext>());
    }

    private Task<int> FirstCategoryId() => InScope((_, db) => db.Categories.Select(c => c.Id).FirstAsync());

    private async Task<int> SeedArticle(ArticleStatus status, string authorUserId = "author-1")
    {
        var categoryId = await FirstCategoryId();
        return await InScope(async (_, db) =>
        {
            var article = new Article
            {
                Title = $"Seed {Guid.NewGuid():N}",
                Slug = $"seed-{Guid.NewGuid():N}",
                Summary = "Tom tat bai seed cho kiem thu.",
                Body = "<p>Noi dung.</p>",
                Status = status,
                CategoryId = categoryId,
                AuthorName = "Tac gia",
                AuthorUserId = authorUserId,
                CreatedAt = DateTime.UtcNow
            };
            db.Articles.Add(article);
            await db.SaveChangesAsync();
            return article.Id;
        });
    }

    private Task<Article> Reload(int id) => InScope((_, db) => db.Articles.AsNoTracking().FirstAsync(a => a.Id == id));
    private Task<int> AuditCount(string action, int articleId) => InScope((_, db) =>
        db.AuditLogs.CountAsync(l => l.EntityType == "Article" && l.EntityId == articleId.ToString() && l.Action == action));

    [Fact]
    public async Task Publish_Success_CommitsArticleAndAuditTogether()
    {
        var id = await SeedArticle(ArticleStatus.PendingReview);

        await InScope(async (svc, _) => { await svc.PublishAsync(id, "editor-1", "Editor"); return 0; });

        Assert.Equal(ArticleStatus.Published, (await Reload(id)).Status);
        Assert.Equal(1, await AuditCount("Publish", id));
    }

    [Fact]
    public async Task Publish_WhenAuditWriteFails_RollsBackTheStatusChange()
    {
        var id = await SeedArticle(ArticleStatus.PendingReview);

        _factory.AuditFailure.Fail = true;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                InScope(async (svc, _) => { await svc.PublishAsync(id, "editor-1", "Editor"); return 0; }));
        }
        finally { _factory.AuditFailure.Fail = false; }

        var after = await Reload(id);
        Assert.Equal(ArticleStatus.PendingReview, after.Status);
        Assert.Null(after.PublishedAt);
        Assert.Null(after.ReviewedByUserId);
        Assert.Equal(0, await AuditCount("Publish", id));
        Assert.Contains(_factory.Logs.Entries, e => e.Level == LogLevel.Error && e.State.Contains($"ArticleId={id}") && e.Message.Contains("rolled back"));
    }

    [Fact]
    public async Task Reject_WhenAuditWriteFails_RollsBackTheStatusChange()
    {
        var id = await SeedArticle(ArticleStatus.PendingReview);

        _factory.AuditFailure.Fail = true;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                InScope(async (svc, _) => { await svc.RejectAsync(id, "editor-1", "Editor", "Thieu nguon"); return 0; }));
        }
        finally { _factory.AuditFailure.Fail = false; }

        var after = await Reload(id);
        Assert.Equal(ArticleStatus.PendingReview, after.Status);
        Assert.Null(after.ReviewNote);
    }

    [Fact]
    public async Task Submit_WhenAuditWriteFails_RollsBack()
    {
        var id = await SeedArticle(ArticleStatus.Draft);

        _factory.AuditFailure.Fail = true;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                InScope(async (svc, _) => { await svc.SubmitForReviewAsync(id, "author-1"); return 0; }));
        }
        finally { _factory.AuditFailure.Fail = false; }

        Assert.Equal(ArticleStatus.Draft, (await Reload(id)).Status);
    }

    [Fact]
    public async Task CreateDraft_WhenAuditWriteFails_LeavesNoArticleBehind()
    {
        var categoryId = await FirstCategoryId();
        var title = $"Khong duoc ton tai {Guid.NewGuid():N}";

        _factory.AuditFailure.Fail = true;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                InScope((svc, _) => svc.CreateDraftAsync(Request(categoryId, title), "author-1", "Tac gia")));
        }
        finally { _factory.AuditFailure.Fail = false; }

        Assert.False(await InScope((_, db) => db.Articles.AnyAsync(a => a.Title == title)));
    }

    [Fact]
    public async Task UpdateDraft_WhenAuditWriteFails_KeepsOriginalContent()
    {
        var id = await SeedArticle(ArticleStatus.Draft);
        var original = await Reload(id);
        var categoryId = await FirstCategoryId();

        _factory.AuditFailure.Fail = true;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                InScope(async (svc, _) => { await svc.UpdateDraftAsync(id, Request(categoryId, "Tieu de moi khong duoc luu"), original.RowVersion, "author-1"); return 0; }));
        }
        finally { _factory.AuditFailure.Fail = false; }

        Assert.Equal(original.Title, (await Reload(id)).Title);
    }

    [Fact]
    public async Task UpdateDraft_WithStaleRowVersion_ThrowsConflict_AndWritesNoAuditRow()
    {
        var id = await SeedArticle(ArticleStatus.Draft);
        var stale = (await Reload(id)).RowVersion;
        var categoryId = await FirstCategoryId();

        await InScope(async (svc, _) => { await svc.UpdateDraftAsync(id, Request(categoryId, "Lan sua thu nhat"), stale, "author-1"); return 0; });
        Assert.Equal(1, await AuditCount("Update", id));

        await Assert.ThrowsAsync<ArticleConcurrencyException>(() =>
            InScope(async (svc, _) => { await svc.UpdateDraftAsync(id, Request(categoryId, "Lan sua thu hai (stale)"), stale, "author-1"); return 0; }));

        Assert.Equal("Lan sua thu nhat", (await Reload(id)).Title);
        Assert.Equal(1, await AuditCount("Update", id));
        Assert.Contains(_factory.Logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Concurrency conflict") && e.State.Contains($"ArticleId={id}"));
    }

    [Fact]
    public async Task AnotherAuthor_CannotUpdateOrSubmit_ButPrivilegedUserCan()
    {
        var id = await SeedArticle(ArticleStatus.Draft, authorUserId: "author-1");
        var rowVersion = (await Reload(id)).RowVersion;
        var categoryId = await FirstCategoryId();

        await Assert.ThrowsAsync<ArticleForbiddenException>(() =>
            InScope(async (svc, _) => { await svc.UpdateDraftAsync(id, Request(categoryId, "Bi chiem quyen"), rowVersion, "author-2"); return 0; }));
        await Assert.ThrowsAsync<ArticleForbiddenException>(() =>
            InScope(async (svc, _) => { await svc.SubmitForReviewAsync(id, "author-2"); return 0; }));
        Assert.Equal(ArticleStatus.Draft, (await Reload(id)).Status);
        Assert.Contains(_factory.Logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Forbidden"));

        await InScope(async (svc, _) => { await svc.SubmitForReviewAsync(id, "editor-1", canManageAll: true); return 0; });
        Assert.Equal(ArticleStatus.PendingReview, (await Reload(id)).Status);
    }

    [Fact]
    public async Task MissingArticle_ThrowsNotFound_ForEveryMutation()
    {
        const int missing = 987654;
        var categoryId = await FirstCategoryId();

        await Assert.ThrowsAsync<ArticleNotFoundException>(() => InScope(async (s, _) => { await s.PublishAsync(missing, "e", "E"); return 0; }));
        await Assert.ThrowsAsync<ArticleNotFoundException>(() => InScope(async (s, _) => { await s.RejectAsync(missing, "e", "E", "ly do"); return 0; }));
        await Assert.ThrowsAsync<ArticleNotFoundException>(() => InScope(async (s, _) => { await s.SubmitForReviewAsync(missing, "e", true); return 0; }));
        await Assert.ThrowsAsync<ArticleNotFoundException>(() => InScope(async (s, _) => { await s.UpdateDraftAsync(missing, Request(categoryId), new byte[8], "e", true); return 0; }));
    }

    [Fact]
    public async Task Reject_RequiresReason_AndUnknownCategoryIsRefused()
    {
        var id = await SeedArticle(ArticleStatus.PendingReview);

        await Assert.ThrowsAsync<ArticleWorkflowException>(() => InScope(async (s, _) => { await s.RejectAsync(id, "e", "E", "   "); return 0; }));
        await Assert.ThrowsAsync<ArticleWorkflowException>(() => InScope(async (s, _) => { await s.RejectAsync(id, "e", "E", new string('x', 1001)); return 0; }));
        Assert.Equal(ArticleStatus.PendingReview, (await Reload(id)).Status);

        await Assert.ThrowsAsync<ArticleWorkflowException>(() =>
            InScope((s, _) => s.CreateDraftAsync(Request(categoryId: 999999), "author-1", "Tac gia")));
    }
}

public class IsolationGuardTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public IsolationGuardTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void TestApp_UsesItsOwnDisposableDatabase_NeverTheDeveloperDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var name = scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Database.GetDbConnection().Database;

        Assert.StartsWith("VNewsTests_", name);
    }
}
