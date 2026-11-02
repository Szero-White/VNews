using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNews.Domain.Identity;
using VNews.Web.Services;

namespace VNews.Web.Controllers;

/// <summary>Newsroom helper for the article editor: keeps a picture that was pasted together with its text by saving a copy on this site.</summary>
[Authorize(Roles = Roles.Admin + "," + Roles.Editor + "," + Roles.Author)]
public sealed class CmsImagesController : Controller
{
    private readonly IRemoteImageFetcher _fetcher;

    public CmsImagesController(IRemoteImageFetcher fetcher) => _fetcher = fetcher;

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Import([FromForm] string? url, CancellationToken cancellationToken)
    {
        if (!RemoteImageFetcher.IsAcceptableUrl(url, out _)) return BadRequest(new { error = "Địa chỉ ảnh không hợp lệ." });

        var result = await _fetcher.ImportAsync(url!, cancellationToken);
        return result.Success ? Ok(new { url = result.Url }) : BadRequest(new { error = result.ErrorMessage });
    }
}
