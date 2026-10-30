using VNews.Application.Common;
using VNews.Application.DTOs;

namespace VNews.Web.Testing;

/// <summary>
/// Deterministic, hand-fixed homepage data used only for visual regression screenshots
/// (enabled via ?visualFixture=1, gated to Development in HomeController). Never touches
/// SQL Server and never affects the normal public homepage, which always reads real data
/// through IArticleQueryService. Same titles, same image files, same counts every call,
/// so repeated Playwright captures are pixel-comparable across sessions.
/// </summary>
public static class VisualFixtureHomePageData
{
    private static readonly (string Name, string Slug)[] Categories =
    {
        ("Thời sự", "thoi-su"), ("Kinh doanh", "kinh-doanh"), ("Thế giới", "the-gioi"),
        ("Công nghệ", "cong-nghe"), ("Thể thao", "the-thao"), ("Giải trí", "giai-tri"),
        ("Sức khỏe", "suc-khoe"), ("Giáo dục", "giao-duc"), ("Du lịch", "du-lich"),
        ("Pháp luật", "phap-luat"),
    };

    private static readonly string[] Authors =
    {
        "Minh Anh", "Hoàng Long", "Thu Trang", "Quốc Bảo", "Lan Phương", "Đức Huy",
    };

    private static readonly string[] HeadlineTemplates =
    {
        "{0} ghi nhận nhiều diễn biến mới trong tuần qua",
        "Bản tin {0} cập nhật những thông tin đáng chú ý nhất",
        "{0}: điểm lại các sự kiện nổi bật gần đây",
        "Chuyên gia nhận định xu hướng {0} trong thời gian tới",
        "{0} tiếp tục thu hút sự quan tâm của độc giả",
        "Những con số đáng chú ý trong lĩnh vực {0} tuần này",
    };

    private static readonly DateTime BaseDate = new(2026, 10, 20, 7, 0, 0, DateTimeKind.Utc);

    private static ArticleSummaryDto Build(int seq, string categoryName, string categorySlug)
    {
        var title = string.Format(HeadlineTemplates[seq % HeadlineTemplates.Length], categoryName);
        return new ArticleSummaryDto
        {
            Id = 900000 + seq,
            Title = title,
            Slug = $"fixture-{categorySlug}-{seq}",
            Summary = $"{title}. Nội dung minh họa cố định phục vụ kiểm thử giao diện trang chủ, không đổi giữa các lần chụp ảnh.",
            CoverImageUrl = $"/images/demo/articles/{(seq % 50) + 1}.jpg",
            AuthorName = Authors[seq % Authors.Length],
            PublishedAt = BaseDate.AddHours(-seq),
            CategoryName = categoryName,
            CategorySlug = categorySlug,
        };
    }

    public static HomePageData Build()
    {
        var seq = 0;
        ArticleSummaryDto Next(string categoryName, string categorySlug) => Build(seq++, categoryName, categorySlug);

        var (heroCat, heroSlug) = Categories[0];
        var hero = Next(heroCat, heroSlug);
        var sideFeatures = new List<ArticleSummaryDto> { Next(Categories[1].Name, Categories[1].Slug), Next(Categories[2].Name, Categories[2].Slug) };
        var mostViewed = Enumerable.Range(0, 5).Select(i => Next(Categories[i % Categories.Length].Name, Categories[i % Categories.Length].Slug)).ToList();
        var latest = Enumerable.Range(0, 10).Select(i => Next(Categories[i % Categories.Length].Name, Categories[i % Categories.Length].Slug)).ToList();
        var streamItems = Enumerable.Range(0, 9).Select(i => Next(Categories[i % Categories.Length].Name, Categories[i % Categories.Length].Slug)).ToList();
        var photoHighlights = Enumerable.Range(0, 8).Select(i => Next(Categories[i % Categories.Length].Name, Categories[i % Categories.Length].Slug)).ToList();
        var spotlight = Enumerable.Range(0, 3).Select(i => Next(Categories[i % Categories.Length].Name, Categories[i % Categories.Length].Slug)).ToList();
        var podcastDemoItems = Enumerable.Range(0, 4).Select(i => Next(Categories[i % Categories.Length].Name, Categories[i % Categories.Length].Slug)).ToList();

        var panels = Categories.Select(c => new CategoryPanel
        {
            Category = new CategoryDto { Id = Array.IndexOf(Categories, c) + 1, Name = c.Name, Slug = c.Slug },
            Articles = Enumerable.Range(0, 5).Select(_ => Next(c.Name, c.Slug)).ToList(),
        }).ToList();

        return new HomePageData
        {
            Hero = hero,
            SideFeatures = sideFeatures,
            Latest = latest,
            StreamItems = streamItems,
            PhotoHighlights = photoHighlights,
            Spotlight = spotlight,
            PodcastDemoItems = podcastDemoItems,
            MostViewed = mostViewed,
            CategoryPanels = panels,
        };
    }
}
