namespace VNews.Application.Interfaces;

/// <summary>
/// Soft delete for articles. Moving an article to the trash only changes its status (the row, slug, PublishedAt, cover image and
/// comments stay), so a restore returns it exactly as it was. Nothing here ever removes data permanently.
/// </summary>
public interface IArticleTrashService
{
    /// <summary>
    /// Authors may trash their own Draft / Rejected articles; editors and admins may trash any article that is not already in the trash,
    /// including Published ones (which leave the public site immediately).
    /// </summary>
    Task MoveToTrashAsync(int articleId, byte[]? rowVersion, string actingUserId, string actingUserName, bool canManageAll, CancellationToken cancellationToken = default);

    /// <summary>Returns the article to the status it had before deletion. Editors/admins restore anything; an author only their own.</summary>
    Task RestoreAsync(int articleId, byte[]? rowVersion, string actingUserId, string actingUserName, bool canManageAll, CancellationToken cancellationToken = default);
}
