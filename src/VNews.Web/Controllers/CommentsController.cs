using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;

namespace VNews.Web.Controllers;

/// <summary>
/// Posting, moderating and withdrawing comments. Every action is a POST protected by the antiforgery token, requires a signed-in
/// user, and is authorized here on the server (the buttons the page shows follow the same rules but are not the protection).
/// </summary>
[Authorize]
public sealed class CommentsController : Controller
{
    private readonly ICommentService _comments;

    public CommentsController(ICommentService comments) => _comments = comments;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private IActionResult BackToArticle(string slug, int? commentId = null, string? anchor = null)
    {
        var url = Url.Action("Detail", "Article", new { slug })!;
        return LocalRedirect($"{url}#{anchor ?? (commentId is int id ? $"comment-{id}" : "binh-luan")}");
    }

    [HttpPost("tin/{slug}/binh-luan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string slug, string? body, int? parentId, CancellationToken cancellationToken)
    {
        var articleId = await _comments.GetPublishedArticleIdAsync(slug, cancellationToken);
        if (articleId is null) return NotFound();

        var name = User.FindFirstValue(Security.DisplayNameClaimsFactory.ClaimType) ?? User.Identity?.Name ?? "Độc giả";
        var result = await _comments.AddAsync(articleId.Value, UserId, name, body ?? string.Empty, parentId, cancellationToken);
        if (!result.Success)
        {
            // Keep what the reader typed so a refusal (too fast, too short) never costs them their text.
            TempData["CommentError"] = result.Message;
            TempData["CommentDraft"] = (body ?? string.Empty).Length > ICommentService.MaxLength ? body![..ICommentService.MaxLength] : body;
            TempData["CommentDraftParent"] = parentId;
            return BackToArticle(slug, anchor: parentId is int p ? $"comment-{p}" : "comment-form");
        }

        TempData["CommentMessage"] = "Đã đăng bình luận của bạn.";
        return BackToArticle(slug, result.CommentId);
    }

    [HttpPost("binh-luan/{id:int}/go")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Editor)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hide(int id, CancellationToken cancellationToken)
    {
        var slug = await _comments.GetArticleSlugAsync(id, cancellationToken);
        if (slug is null) return NotFound();

        var name = User.FindFirstValue(Security.DisplayNameClaimsFactory.ClaimType) ?? User.Identity?.Name ?? "Biên tập viên";
        var result = await _comments.HideAsync(id, UserId, name, cancellationToken);
        TempData[result.Success ? "CommentMessage" : "CommentError"] = result.Success ? "Đã gỡ bình luận." : result.Message;
        return BackToArticle(slug);
    }

    [HttpPost("binh-luan/{id:int}/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOwn(int id, CancellationToken cancellationToken)
    {
        var slug = await _comments.GetArticleSlugAsync(id, cancellationToken);
        if (slug is null) return NotFound();

        var result = await _comments.DeleteOwnAsync(id, UserId, cancellationToken);
        if (result.Failure == CommentFailure.Forbidden) return Forbid();
        TempData[result.Success ? "CommentMessage" : "CommentError"] = result.Success ? "Đã xóa bình luận của bạn." : result.Message;
        return BackToArticle(slug);
    }
}
