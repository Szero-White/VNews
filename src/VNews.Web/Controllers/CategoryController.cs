using Microsoft.AspNetCore.Mvc;
using VNews.Application.Common;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;
using VNews.Web.Models;

namespace VNews.Web.Controllers;

public class CategoryController : Controller
{
    private const int PageSize = 12;

    /// <summary>The lead story plus three headline cards at the top of page 1.</summary>
    private const int FeaturedCount = 4;
    private const int GroupSize = 4;

    private readonly IArticleQueryService _articleQueryService;
    private readonly ICategoryQueryService _categoryQueryService;

    public CategoryController(IArticleQueryService articleQueryService, ICategoryQueryService categoryQueryService)
    {
        _articleQueryService = articleQueryService;
        _categoryQueryService = categoryQueryService;
    }

    public async Task<IActionResult> Index(string slug, int page = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var category = await _categoryQueryService.GetBySlugAsync(slug, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        var layout = category.Children.Count > 0
            ? await BuildParentLayoutAsync(category, page, cancellationToken)
            : await BuildPlainLayoutAsync(category, page, cancellationToken);

        layout.Category = category;
        layout.MostViewed = await _articleQueryService.GetMostViewedAsync(5, null, cancellationToken);
        layout.SubCategoryLinks = await SubCategoryLinksAsync(category, cancellationToken);
        return View(layout);
    }

    /// <summary>A category without sub-categories: page 1 is the lead + cards taken from the top of the feed, then the rest of the feed.</summary>
    private async Task<CategoryPageViewModel> BuildPlainLayoutAsync(CategoryDto category, int page, CancellationToken cancellationToken)
    {
        var articles = await LoadFeedAsync(category.Slug, page, null, cancellationToken);
        var items = articles.Items;
        var firstPage = articles.Page == 1;
        return new CategoryPageViewModel
        {
            Articles = articles,
            Lead = firstPage ? items.FirstOrDefault() : null,
            Cards = firstPage ? items.Skip(1).Take(FeaturedCount - 1).ToList() : Array.Empty<ArticleSummaryDto>(),
            Rows = items.Skip(firstPage ? FeaturedCount : 0).ToList(),
        };
    }

    /// <summary>
    /// A parent category. Page 1 shows the newest stories as lead + cards, then one block per sub-category that has stories. Everything shown in
    /// those places is removed from the feed on EVERY page (before paging), so no story appears twice on a page or on two pages.
    /// </summary>
    private async Task<CategoryPageViewModel> BuildParentLayoutAsync(CategoryDto category, int page, CancellationToken cancellationToken)
    {
        var featured = (await _articleQueryService.GetByCategoryAsync(category.Slug, 1, FeaturedCount, cancellationToken)).Items;
        var groups = await _articleQueryService.GetSubCategoryGroupsAsync(category.Slug, GroupSize, featured.Select(a => a.Id).ToList(), cancellationToken);
        var shownAbove = featured.Select(a => a.Id).Concat(groups.SelectMany(g => g.Articles).Select(a => a.Id)).ToList();

        var feed = await LoadFeedAsync(category.Slug, page, shownAbove, cancellationToken);
        var firstPage = feed.Page == 1;
        return new CategoryPageViewModel
        {
            Articles = feed,
            Lead = firstPage ? featured.FirstOrDefault() : null,
            Cards = firstPage ? featured.Skip(1).ToList() : Array.Empty<ArticleSummaryDto>(),
            Groups = firstPage ? groups : Array.Empty<CategoryGroupDto>(),
            Rows = feed.Items,
        };
    }

    /// <summary>One page of the feed. A page number past the end (stale link, hand-edited URL) shows the last real page, not an empty one.</summary>
    private async Task<PagedResult<ArticleSummaryDto>> LoadFeedAsync(string slug, int page, IReadOnlyCollection<int>? excludeIds, CancellationToken cancellationToken)
    {
        var articles = await _articleQueryService.GetByCategoryAsync(slug, page, PageSize, cancellationToken, excludeIds);
        if (articles.Items.Count == 0 && articles.TotalCount > 0 && page > articles.TotalPages)
        {
            articles = await _articleQueryService.GetByCategoryAsync(slug, articles.TotalPages, PageSize, cancellationToken, excludeIds);
        }

        return articles;
    }

    /// <summary>The links under the title: this category's children, or the siblings when this is a sub-category.</summary>
    private async Task<IReadOnlyList<CategoryDto>> SubCategoryLinksAsync(CategoryDto category, CancellationToken cancellationToken)
    {
        if (!category.IsChild) return category.Children;
        var parent = await _categoryQueryService.GetBySlugAsync(category.ParentSlug!, cancellationToken);
        return parent?.Children ?? Array.Empty<CategoryDto>();
    }
}
