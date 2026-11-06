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
        try
        {
            Directory.CreateDirectory(Path.Combine(Root, "articles"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A host that keeps pictures off-site (Cloudinary) may not let the site write next to its own files. Start anyway: nothing
            // is stored here in that mode, and the file serving below is skipped while the folder does not exist.
        }
    }

    /// <summary>The folder that /uploads maps to.</summary>
    public string Root { get; }

    /// <summary>Article pictures (/uploads/articles/...).</summary>
    public string ArticlesPath => Path.Combine(Root, "articles");
}
