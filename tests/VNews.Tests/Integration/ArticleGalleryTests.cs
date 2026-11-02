using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>The pictures inside an article: any number, kept in order, replaced on save, only this site's own uploads, and never lost to the unused-upload clean-up.</summary>
public sealed class ArticleGalleryTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public ArticleGalleryTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private static string Url(char c) => $"/uploads/articles/{new string(c, 32)}.png";

    private static ArticleSaveRequest Request(int categoryId, params string[] gallery) => new()
    {
        Title = "Bài có nhiều ảnh " + Guid.NewGuid().ToString("N")[..6], Summary = "Tóm tắt đủ dài", Body = "<p>Nội dung đủ dài để hợp lệ.</p>",
        CategoryId = categoryId, GalleryImages = gallery.ToList(),
    };

    private async Task<int> CategoryIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Categories.Select(c => c.Id).FirstAsync();
    }

    [Fact]
    public async Task Gallery_IsSavedInOrder_ReplacedOnUpdate_AndEmptyIsFine()
    {
        var category = await CategoryIdAsync();
        using var scope = _factory.Services.CreateScope();
        var commands = scope.ServiceProvider.GetRequiredService<IArticleCommandService>();
        var query = scope.ServiceProvider.GetRequiredService<IArticleCmsQueryService>();

        var id = await commands.CreateDraftAsync(Request(category, Url('a'), Url('b'), Url('c')), "author-1", "Tác giả");
        Assert.Equal(new[] { Url('a'), Url('b'), Url('c') }, (await query.GetForEditAsync(id))!.GalleryImages);

        var edit = (await query.GetForEditAsync(id))!;
        await commands.UpdateAsync(id, Request(category, Url('c'), Url('d')), edit.RowVersion, "author-1", canManageAll: false);
        Assert.Equal(new[] { Url('c'), Url('d') }, (await query.GetForEditAsync(id))!.GalleryImages);

        edit = (await query.GetForEditAsync(id))!;
        await commands.UpdateAsync(id, Request(category), edit.RowVersion, "author-1", canManageAll: false); // all pictures removed
        Assert.Empty((await query.GetForEditAsync(id))!.GalleryImages);

        var none = await commands.CreateDraftAsync(Request(category), "author-1", "Tác giả"); // an article without any picture
        Assert.Empty((await query.GetForEditAsync(none))!.GalleryImages);
    }

    [Fact]
    public void Validation_AcceptsOnlyOwnUploads_DropsEmptyBoxes_AndCapsTheCount()
    {
        static IReadOnlyList<ValidationResult> Check(ArticleSaveRequest r) => r.Validate(new ValidationContext(r)).ToList();

        Assert.Empty(Check(Request(1, Url('a'), "", "   ", Url('b'))));
        var withEmpties = Request(1, Url('a'), "", Url('b'));
        Check(withEmpties);
        Assert.Equal(2, withEmpties.GalleryImages.Count); // empty boxes are dropped, not an error

        Assert.NotEmpty(Check(Request(1, "https://evil.example/x.png")));
        Assert.NotEmpty(Check(Request(1, "/uploads/articles/../secret.png")));
        Assert.NotEmpty(Check(Request(1, "/uploads/articles/ABC.png")));
        Assert.NotEmpty(Check(Request(1, Enumerable.Range(0, ArticleSaveRequest.MaxGalleryImages + 1).Select(i => Url((char)('a' + i % 6))).ToArray())));
    }

    [Fact]
    public async Task PublicPage_ShowsTheGalleryInOrder_WithoutOneWhenThereAreNoPictures()
    {
        var category = await CategoryIdAsync();
        int withPictures, without;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
            Article Make(string slug) => new() { Title = slug, Slug = slug + Guid.NewGuid().ToString("N")[..6], Summary = "s", Body = "<p>b</p>", AuthorName = "A", Status = ArticleStatus.Published, PublishedAt = DateTime.UtcNow, CategoryId = category };
            var a = Make("co-anh"); var b = Make("khong-anh");
            a.Images = new List<ArticleImage> { new() { Url = Url('2'), SortOrder = 1 }, new() { Url = Url('1'), SortOrder = 0 } };
            db.Articles.AddRange(a, b);
            await db.SaveChangesAsync();
            withPictures = a.Id; without = b.Id;
            using var client = _factory.CreateClient();
            var html = await client.GetDecodedAsync($"/tin/{a.Slug}");
            Assert.True(html.IndexOf(Url('1'), StringComparison.Ordinal) < html.IndexOf(Url('2'), StringComparison.Ordinal));
            Assert.Contains("article-gallery--2", html);
            Assert.DoesNotContain("article-gallery", await client.GetDecodedAsync($"/tin/{b.Slug}"));
        }

        Assert.NotEqual(withPictures, without);
    }

    [Fact]
    public async Task CoverCaption_IsKeptWhenSet_DefaultsWhenEmpty_AndDropsWithTheCover()
    {
        var category = await CategoryIdAsync();
        using var scope = _factory.Services.CreateScope();
        var commands = scope.ServiceProvider.GetRequiredService<IArticleCommandService>();
        var query = scope.ServiceProvider.GetRequiredService<IArticleCmsQueryService>();

        ArticleSaveRequest With(string? cover, string? caption) { var r = Request(category); r.CoverImageUrl = cover; r.CoverCaption = caption; return r; }
        var id = await commands.CreateDraftAsync(With(Url('a'), "  Cầu Rồng về đêm. Ảnh: Nhân vật  "), "author-1", "Tác giả");
        Assert.Equal("Cầu Rồng về đêm. Ảnh: Nhân vật", (await query.GetForEditAsync(id))!.CoverCaption); // trimmed

        var edit = (await query.GetForEditAsync(id))!;
        await commands.UpdateAsync(id, With(Url('a'), "   "), edit.RowVersion, "author-1", canManageAll: false);
        Assert.Null((await query.GetForEditAsync(id))!.CoverCaption); // empty = the default text on the page

        edit = (await query.GetForEditAsync(id))!;
        await commands.UpdateAsync(id, With(Url('a'), "Chú thích mới"), edit.RowVersion, "author-1", canManageAll: false);
        edit = (await query.GetForEditAsync(id))!;
        await commands.UpdateAsync(id, With(null, "Chú thích mồ côi"), edit.RowVersion, "author-1", canManageAll: false); // cover removed
        Assert.Null((await query.GetForEditAsync(id))!.CoverCaption);
    }

    [Fact]
    public async Task ArticlePage_ShowsTheCaptionOrTheDefault()
    {
        var category = await CategoryIdAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        Article Make(string caption) => new() { Title = "t", Slug = "chu-thich-" + Guid.NewGuid().ToString("N")[..8], Summary = "s", Body = "<p>b</p>", AuthorName = "A", Status = ArticleStatus.Published, PublishedAt = DateTime.UtcNow, CategoryId = category, CoverImageUrl = Url('a'), CoverCaption = caption };
        var custom = Make("Chợ nổi buổi sớm"); var plain = Make(null!);
        db.Articles.AddRange(custom, plain);
        await db.SaveChangesAsync();

        using var client = _factory.CreateClient();
        Assert.Contains("<figcaption>Chợ nổi buổi sớm</figcaption>", await client.GetDecodedAsync($"/tin/{custom.Slug}"));
        var html = await client.GetDecodedAsync($"/tin/{plain.Slug}");
        Assert.Contains("<figcaption>Ảnh minh họa</figcaption>", html);
    }
}
