using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>The activity-log filters and the moderation list: narrowing, counts, real states after an action, and dates.</summary>
public sealed class AuditAndCommentFilterTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public AuditAndCommentFilterTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task AuditLog_FiltersByActionObjectPersonTextAndDay_AndOffersTheValuesThatExist()
    {
        var marker = "nk" + Guid.NewGuid().ToString("N")[..8];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var early = new DateTime(2026, 2, 10, 20, 0, 0, DateTimeKind.Utc); // 11 Feb 03:00 in Vietnam
        db.AuditLogs.AddRange(
            new AuditLog { UserId = "u1", UserDisplayName = "Người A " + marker, Action = "Trash", EntityType = "Article", EntityId = "1", Detail = "Xóa bài " + marker, CreatedAt = early },
            new AuditLog { UserId = "u2", UserDisplayName = "Người B " + marker, Action = "HideComment", EntityType = "Comment", EntityId = "2", Detail = "Gỡ bình luận " + marker, CreatedAt = early.AddDays(5) });
        await db.SaveChangesAsync();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLogQueryService>();

        Assert.Equal(2, (await audit.GetPageAsync(new AuditFilter(Search: marker), 1, 50)).TotalCount);
        Assert.Single((await audit.GetPageAsync(new AuditFilter(Action: "Trash", Search: marker), 1, 50)).Items);
        Assert.Single((await audit.GetPageAsync(new AuditFilter(EntityType: "Comment", Search: marker), 1, 50)).Items);
        Assert.Single((await audit.GetPageAsync(new AuditFilter(User: "Người B " + marker), 1, 50)).Items);
        Assert.Single((await audit.GetPageAsync(new AuditFilter(Search: "xoa bai " + marker), 1, 50)).Items); // accent-insensitive

        // the day picked in Vietnam time: 11 Feb in Vietnam starts at 10 Feb 17:00 UTC and contains the first entry (03:00 local)
        var day = await audit.GetPageAsync(new AuditFilter(Search: marker, FromUtc: new DateTime(2026, 2, 10, 17, 0, 0, DateTimeKind.Utc), ToUtcExclusive: new DateTime(2026, 2, 11, 17, 0, 0, DateTimeKind.Utc)), 1, 50);
        Assert.Equal("Trash", Assert.Single(day.Items).Action);

        var options = await audit.GetFilterOptionsAsync();
        Assert.Contains("Trash", options.Actions);
        Assert.Contains("Comment", options.EntityTypes);
        Assert.Contains("Người A " + marker, options.Users);
        Assert.Equal(2, (await audit.GetPageAsync(1, 50)).Items.Count(a => a.UserDisplayName.EndsWith(marker))); // the unfiltered overload still works
    }

    [Fact]
    public async Task CommentModeration_CountsStates_ReportsTheRealStateAfterAnAction_AndFiltersByDay()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var article = new Article
        {
            Title = "Bài để bình luận", Slug = "binh-luan-" + Guid.NewGuid().ToString("N")[..8], Summary = "s", Body = "<p>b</p>", AuthorName = "A",
            Status = ArticleStatus.Published, PublishedAt = DateTime.UtcNow, CategoryId = await db.Categories.Select(c => c.Id).FirstAsync(),
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        var when = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var visible = new Comment { ArticleId = article.Id, UserId = "u1", AuthorName = "Bạn đọc", Body = "Bình luận hiển thị", CreatedAtUtc = when };
        var deleted = new Comment { ArticleId = article.Id, UserId = "u2", AuthorName = "Bạn đọc 2", Body = "Bình luận người viết đã xóa", Status = CommentStatus.Deleted, CreatedAtUtc = when };
        db.Comments.AddRange(visible, deleted);
        await db.SaveChangesAsync();

        var moderation = scope.ServiceProvider.GetRequiredService<ICommentModerationService>();
        var comments = scope.ServiceProvider.GetRequiredService<ICommentService>();
        var before = await moderation.CountsAsync();

        await comments.HideAsync(visible.Id, "ed", "Biên tập");
        var states = await moderation.StatesAsync(new[] { visible.Id, deleted.Id });
        Assert.Equal("Hidden", states[visible.Id]);
        Assert.Equal("Deleted", states[deleted.Id]); // a deleted comment never turns into anything else

        var after = await moderation.CountsAsync();
        Assert.Equal(before["Visible"] - 1, after["Visible"]);
        Assert.Equal(before["Hidden"] + 1, after["Hidden"]);

        var inRange = await moderation.ListAsync(null, "bình luận", 1, 50, default, new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(2, inRange.Items.Count(i => i.ArticleId == article.Id));
        var outOfRange = await moderation.ListAsync(null, "bình luận", 1, 50, default, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), null);
        Assert.DoesNotContain(outOfRange.Items, i => i.ArticleId == article.Id);
    }
}
