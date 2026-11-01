namespace VNews.Application.Common;

/// <summary>Base type for "the requested record does not exist" so one handler can map all of them to HTTP 404.</summary>
public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string message) : base(message) { }
}

public class ArticleNotFoundException : EntityNotFoundException
{
    public ArticleNotFoundException(int id) : base($"Không tìm thấy bài viết #{id}.") { }
}

public class CategoryNotFoundException : EntityNotFoundException
{
    public CategoryNotFoundException(int id) : base($"Không tìm thấy chuyên mục #{id}.") { }
}

/// <summary>A business rule rejected the operation (e.g. wrong article state, duplicate slug). Maps to HTTP 409.</summary>
public class ArticleWorkflowException : Exception
{
    public ArticleWorkflowException(string message) : base(message) { }
}

/// <summary>The acting user is not allowed to touch this record. Maps to HTTP 403.</summary>
public class ArticleForbiddenException : Exception
{
    public ArticleForbiddenException(string message) : base(message) { }
}

/// <summary>Optimistic concurrency conflict (RowVersion mismatch). Maps to HTTP 409.</summary>
public class ArticleConcurrencyException : Exception
{
    public ArticleConcurrencyException()
        : base("Bài viết đã bị thay đổi bởi người khác. Vui lòng tải lại và thử lại.") { }
}
