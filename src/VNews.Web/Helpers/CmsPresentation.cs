namespace VNews.Web.Helpers;

/// <summary>
/// Presentation of the article workflow. The stored statuses (Draft, PendingReview, Rejected, Published) are unchanged;
/// only the Vietnamese wording and the badge style are decided here.
/// </summary>
public static class CmsStatus
{
    public static string Label(string status) => status switch
    {
        "Draft" => "Bản nháp",
        "PendingReview" => "Chờ duyệt",
        "Rejected" => "Cần chỉnh sửa", // an editor sent it back with a reason: the author must edit it
        "Published" => "Đã xuất bản",
        "Trashed" => "Thùng rác",
        _ => status,
    };

    public static string Css(string status) => status switch
    {
        "Draft" => "draft",
        "PendingReview" => "pending",
        "Rejected" => "rejected",
        "Published" => "published",
        "Trashed" => "trashed",
        _ => "draft",
    };

    public static string Hint(string status) => status switch
    {
        "Draft" => "Đang soạn, chưa gửi duyệt",
        "PendingReview" => "Đang chờ biên tập viên xử lý",
        "Rejected" => "Bị trả lại kèm lý do, cần sửa rồi gửi duyệt lại",
        "Published" => "Đang hiển thị công khai",
        "Trashed" => "Đã xóa khỏi danh sách, có thể khôi phục",
        _ => string.Empty,
    };

    /// <summary>The filter values the list accepts, in the order they are offered.</summary>
    public static readonly string[] All = { "Draft", "PendingReview", "Rejected", "Published", "Trashed" };
}

/// <summary>
/// What a user may do with one article, decided by the same rules the server enforces (so the page never offers an
/// action the server would refuse). The server still re-checks every request.
/// </summary>
public sealed record CmsRowActions(bool CanViewPublic, bool CanEdit, bool CanSubmit, bool CanPublish, bool CanReject, bool CanTrash, bool CanRestore)
{
    public bool Any => CanViewPublic || CanEdit || CanSubmit || CanPublish || CanReject || CanTrash || CanRestore;

    public static CmsRowActions For(string status, bool isOwner, bool isPrivileged)
    {
        var mayChange = isOwner || isPrivileged;
        var editable = status is "Draft" or "Rejected";
        // A published article may be edited by editors and admins only (the service enforces the same rule).
        var canEditPublished = status == "Published" && isPrivileged;
        return new CmsRowActions(
            CanViewPublic: status == "Published",
            CanEdit: (editable && mayChange) || canEditPublished,
            CanSubmit: editable && mayChange,
            CanPublish: status == "PendingReview" && isPrivileged,
            CanReject: status == "PendingReview" && isPrivileged,
            // Same rule as ArticleTrashService: an author discards only their own draft; editors/admins anything not yet in the trash.
            CanTrash: status != "Trashed" && (isPrivileged || (editable && isOwner)),
            CanRestore: status == "Trashed" && mayChange);
    }
}

public static class AppFormat
{
    public static string Initials(string? name)
    {
        var parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => (parts[0][..1] + parts[^1][..1]).ToUpperInvariant(),
        };
    }

    public static string RoleLabel(string role) => role switch
    {
        "Admin" => "Quản trị viên",
        "Editor" => "Biên tập viên",
        "Author" => "Tác giả",
        "Reader" => "Độc giả",
        _ => role,
    };

    /// <summary>The most privileged role first (Admin, Editor, Author, Reader).</summary>
    public static string PrimaryRole(IEnumerable<string> roles)
    {
        var set = roles.ToHashSet();
        foreach (var role in new[] { "Admin", "Editor", "Author", "Reader" })
        {
            if (set.Contains(role)) return role;
        }
        return "Reader";
    }
}

public static class AuditLabels
{
    public static string Action(string action) => action switch
    {
        "Create" => "Tạo bản nháp",
        "Update" => "Cập nhật bài",
        "SubmitForReview" => "Gửi duyệt",
        "Publish" => "Xuất bản",
        "Reject" => "Từ chối",
        "UpdatePublished" => "Sửa bài đã xuất bản",
        "HideComment" => "Gỡ bình luận",
        "RestoreComment" => "Khôi phục bình luận",
        "DeleteCategory" => "Xóa chuyên mục",
        "ImportNews" => "Nạp tin từ nguồn",
        "ChangeLockout" => "Khóa / mở khóa tài khoản",
        "Trash" => "Xóa bài (thùng rác)",
        "Restore" => "Khôi phục bài",
        "QuarantineUploads" => "Dọn ảnh không dùng",
        _ => action,
    };

    public static string Entity(string entity) => entity switch
    {
        "Article" => "Bài viết",
        "Comment" => "Bình luận",
        "Upload" => "Ảnh tải lên",
        "Category" => "Chuyên mục",
        "User" => "Người dùng",
        _ => entity,
    };
}
