using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;
using VNews.Web.Helpers;
using VNews.Web.Models;

namespace VNews.Web.Controllers;

public class ArticleController : Controller
{
    private const int CommentsPerPage = 10;
    private readonly IArticleQueryService _articleQueryService;
    private readonly ICommentService _comments;

    public ArticleController(IArticleQueryService articleQueryService, ICommentService comments)
    {
        _articleQueryService = articleQueryService;
        _comments = comments;
    }

    public async Task<IActionResult> Detail(string slug, int cpage = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var article = await _articleQueryService.GetBySlugAsync(slug, cancellationToken);
        if (article is null)
        {
            return NotFound();
        }

        HttpContext.Items[VNews.Web.ViewComponents.CategoryNavViewComponent.ActiveCategoryItemKey] = article.CategorySlug;
        var related = await _articleQueryService.GetByCategoryAsync(article.CategorySlug, 1, 8, cancellationToken);
        var mostViewed = await _articleQueryService.GetMostViewedAsync(5, article.Id, cancellationToken);
        var neighbors = await _articleQueryService.GetNeighborsAsync(article.Id, cancellationToken);
        var comments = await _comments.GetThreadsAsync(article.Id, cpage, CommentsPerPage, cancellationToken);
        return View(new ArticleDetailPageViewModel
        {
            Article = article,
            Neighbors = neighbors,
            Comments = comments,
            ReadMinutes = ReadingTime.Minutes(article.Body),
            IsSignedIn = User.Identity?.IsAuthenticated == true,
            CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            CanModerate = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Editor),
            CommentError = TempData["CommentError"] as string,
            CommentMessage = TempData["CommentMessage"] as string,
            CommentDraft = TempData["CommentDraft"] as string,
            CommentDraftParent = TempData["CommentDraftParent"] as int?,
            RelatedArticles = related.Items.Where(item => item.Id != article.Id).Take(4).ToArray(),
            MostViewed = mostViewed
        });
    }
}
