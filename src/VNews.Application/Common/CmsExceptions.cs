namespace VNews.Application.Common;

public class ArticleNotFoundException : Exception
{
    public ArticleNotFoundException(int id) : base($"Không tìm thấy bài viết #{id}.") { }
}

public class ArticleWorkflowException : Exception
{
    public ArticleWorkflowException(string message) : base(message) { }
}

public class ArticleForbiddenException : Exception
{
    public ArticleForbiddenException(string message) : base(message) { }
}

public class ArticleConcurrencyException : Exception
{
    public ArticleConcurrencyException()
        : base("Bài viết đã bị thay đổi bởi người khác. Vui lòng tải lại và thử lại.") { }
}
