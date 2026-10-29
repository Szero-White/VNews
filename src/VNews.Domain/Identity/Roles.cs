namespace VNews.Domain.Identity;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Author = "Author";
    public const string Reader = "Reader";

    public static readonly string[] All = { Admin, Editor, Author, Reader };
}
