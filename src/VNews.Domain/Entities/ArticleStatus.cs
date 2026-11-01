namespace VNews.Domain.Entities;

public enum ArticleStatus
{
    Draft = 0,
    PendingReview = 1,
    Published = 2,
    Rejected = 3,

    /// <summary>Soft-deleted: hidden everywhere (public and CMS lists) except the trash, and restorable. Never removed from the database.</summary>
    Trashed = 4
}
