namespace VNews.Web.Services;

/// <summary>
/// Where uploaded pictures live on disk. One definition for the upload, the clean-up and the file serving, so they can never disagree.
/// The default is wwwroot/uploads; set Uploads:RootPath to keep pictures outside the application folder, so publishing a new build
/// does not lose what editors have uploaded.
/// </summary>
public sealed class UploadStorage
{
    public const string RequestPath = "/uploads";

    public UploadStorage(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configured = configuration["Uploads:RootPath"];
        Root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.WebRootPath, "uploads")
            : Path.GetFullPath(configured);
        Directory.CreateDirectory(Path.Combine(Root, "articles"));
    }

    /// <summary>The folder that /uploads maps to.</summary>
    public string Root { get; }

    /// <summary>Article pictures (/uploads/articles/...).</summary>
    public string ArticlesPath => Path.Combine(Root, "articles");
}
