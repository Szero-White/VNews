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
    private readonly IAnalyticsService _analytics;
    private readonly ILogger<ArticleController> _logger;

    public ArticleController(IArticleQueryService articleQueryService, ICommentService comments, IAnalyticsService analytics, ILogger<ArticleController> logger)
    {
        _articleQueryService = articleQueryService;
        _comments = comments;
        _analytics = analytics;
        _logger = logger;
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

        await RecordViewAsync(article.Id, cancellationToken);
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

    /// <summary>Counts a real reading for the statistics. Crawlers are ignored, and a failure here never stops the reader from getting the article.</summary>
    private async Task RecordViewAsync(int articleId, CancellationToken cancellationToken)
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        if (VisitorFingerprint.IsCrawler(userAgent)) return;
        try
        {
            await _analytics.RecordViewAsync(articleId, VisitorFingerprint.Hash(HttpContext, DateTime.UtcNow), VisitorFingerprint.IsMobile(userAgent), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not record a view of article {ArticleId}.", articleId);
        }
    }
}
