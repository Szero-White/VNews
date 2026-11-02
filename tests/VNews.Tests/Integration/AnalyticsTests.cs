using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;
using VNews.Web.Helpers;

namespace VNews.Tests.Integration;

/// <summary>Reading statistics: reload dedup, anonymous counting, the report's totals/top lists, and crawler detection.</summary>
public sealed class AnalyticsTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public AnalyticsTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task RecordView_IgnoresAReloadWithin30Minutes_ButCountsAnotherReaderAndAnotherArticle()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
        var ids = await db.Articles.Select(a => a.Id).Take(2).ToListAsync();
        var hash = Guid.NewGuid().ToString("N");
        var before = await db.ArticleViews.CountAsync();

        Assert.True(await analytics.RecordViewAsync(ids[0], hash, isMobile: false));
        Assert.False(await analytics.RecordViewAsync(ids[0], hash, isMobile: false)); // reload
        Assert.True(await analytics.RecordViewAsync(ids[0], Guid.NewGuid().ToString("N"), isMobile: true)); // another reader
        Assert.True(await analytics.RecordViewAsync(ids[1], hash, isMobile: false)); // same reader, another article
        Assert.False(await analytics.RecordViewAsync(ids[0], "", isMobile: false)); // no visitor, nothing recorded

        Assert.Equal(before + 3, await db.ArticleViews.CountAsync());
    }

    [Fact]
    public async Task Report_CountsTodaysViewsReadersDevicesAndRanksTheArticle()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
        var article = await db.Articles.Include(a => a.Category).FirstAsync();
        var before = await analytics.GetReportAsync(7);

        await analytics.RecordViewAsync(article.Id, Guid.NewGuid().ToString("N"), isMobile: true);
        await analytics.RecordViewAsync(article.Id, Guid.NewGuid().ToString("N"), isMobile: false);
        var after = await analytics.GetReportAsync(7);

        Assert.Equal(7, after.Daily.Count);
        Assert.Equal(before.Current.Views + 2, after.Current.Views);
        Assert.Equal(before.Current.Visitors + 2, after.Current.Visitors);
        Assert.Equal(before.MobileViews + 1, after.MobileViews);
        Assert.Equal(before.DesktopViews + 1, after.DesktopViews);
        Assert.Equal(24, after.ViewsByHour.Count);
        Assert.Equal(after.Current.Views, after.ViewsByHour.Sum());
        Assert.Contains(after.TopCategories, c => c.Name == article.Category.Name);
        Assert.NotNull(after.TrackingSinceUtc);
    }

    [Fact]
    public async Task Report_DoesNotCountViewsOlderThanThePeriod_IntoTheCurrentPeriod()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
        var articleId = await db.Articles.Select(a => a.Id).FirstAsync();
        var before = await analytics.GetReportAsync(7);

        db.ArticleViews.Add(new ArticleView { ArticleId = articleId, VisitorHash = Guid.NewGuid().ToString("N"), ViewedAtUtc = DateTime.UtcNow.AddDays(-10) });
        await db.SaveChangesAsync();
        var after = await analytics.GetReportAsync(7);

        Assert.Equal(before.Current.Views, after.Current.Views);
        Assert.Equal(before.Previous.Views + 1, after.Previous.Views); // 10 days ago is in the 7 days before the last 7
    }

    [Theory]
    [InlineData("Googlebot/2.1 (+http://www.google.com/bot.html)", true)]
    [InlineData("Mozilla/5.0 HeadlessChrome/120", true)]
    [InlineData("curl/8.4.0", true)]
    [InlineData("", true)]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36", false)]
    public void Crawlers_AreRecognised(string userAgent, bool expected) => Assert.Equal(expected, VisitorFingerprint.IsCrawler(userAgent));

    [Theory]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) Mobile/15E148", true)]
    [InlineData("Mozilla/5.0 (Linux; Android 14) Mobile Safari/537.36", true)]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120", false)]
    public void MobileAgents_AreRecognised(string userAgent, bool expected) => Assert.Equal(expected, VisitorFingerprint.IsMobile(userAgent));
}
