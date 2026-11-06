using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Data.SqlClient;

// VNews.ImageMigrator: copies the article pictures to Cloudinary and checks the result. Three commands:
//   plan    reads the pictures folder (and, when given, the database) and reports what would be copied. Touches nothing, needs no account.
//   upload  copies what Cloudinary does not have yet. Safe to stop and run again. Needs --yes.
//   verify  compares Cloudinary with the plan and opens every delivery URL.
// It never writes to the database, never changes or deletes a local file, and never overwrites a picture already on Cloudinary.
// Credentials come only from the CLOUDINARY_URL environment variable (cloudinary://key:secret@cloud) and are never printed.

return await Tool.RunAsync(args);

internal static class Tool
{
    private const long CloudinaryFreeImageLimit = 10L * 1024 * 1024;
    private static readonly Regex ReferencePattern = new("/uploads/articles/(?<id>[0-9a-f]{32})\\.(?<ext>jpg|png|webp)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex FileNamePattern = new("^(?<id>[0-9a-f]{32})\\.(?<ext>jpg|png|webp)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex UrlPattern = new("^cloudinary://(?<key>[^:@/\\s]+):(?<secret>[^@/\\s]+)@(?<cloud>[A-Za-z0-9_-]+)/?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private sealed record Local(string Id, string Ext, string Path, long Bytes, string Sha256);

    private sealed class Plan
    {
        public List<Local> Expected { get; } = new();                 // what must end up on Cloudinary
        public List<string> MissingLocally { get; } = new();          // referenced by the database, no file on disk
        public List<string> Unreferenced { get; } = new();            // a file on disk the database never mentions
        public List<string> ExtensionMismatch { get; } = new();       // same id on disk with another extension than the database says
        public Dictionary<string, string> Source { get; } = new();    // id -> where it is used (cover / gallery / body)
        public bool FromDatabase { get; set; }
        public int IgnoredFiles { get; set; }
        public int LocalFiles { get; set; }
    }

    private sealed class Options
    {
        public string Command = "";
        public string Uploads = "";
        public string Report = "migration-report";
        public string Folder = "vnews/articles";
        public string Transform = "f_auto,q_auto";
        public string DbEnv = "VNEWS_DB_CONNECTION";
        public int Limit = int.MaxValue;
        public bool Yes;
        public bool IncludeUnreferenced;
    }

    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryParse(args, out var o, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(Usage);
            return 2;
        }

        try
        {
            var plan = await BuildPlanAsync(o);
            Directory.CreateDirectory(o.Report);
            WritePlan(o, plan);
            PrintPlan(o, plan);
            if (plan.MissingLocally.Count > 0 || plan.ExtensionMismatch.Count > 0)
            {
                Console.Error.WriteLine("\nSTOP: some referenced pictures have no matching file on disk. Fix that first (see plan.csv / problems.txt).");
                return 1;
            }

            return o.Command switch
            {
                "plan" => 0,
                "upload" => await UploadAsync(o, plan),
                "verify" => await VerifyAsync(o, plan),
                _ => 2,
            };
        }
        catch (Exception ex) when (ex is IOException or SqlException or HttpRequestException or InvalidOperationException)
        {
            // The message never contains the credentials: they are not part of any text this tool builds.
            Console.Error.WriteLine($"FAILED: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private const string Usage = """
        Usage:
          VNews.ImageMigrator plan   --uploads <folder> [--db-env VAR] [--report <dir>]
          VNews.ImageMigrator upload --uploads <folder> --yes [--limit N] [--db-env VAR] [--report <dir>]
          VNews.ImageMigrator verify --uploads <folder> [--db-env VAR] [--report <dir>]
        Options:
          --uploads <folder>      the folder that contains "articles" (the original /uploads), or the "articles" folder itself
          --db-env VAR            name of the environment variable holding a SQL connection string (default VNEWS_DB_CONNECTION). When it
                                  is set, only pictures the database references are copied and every reference is checked against disk.
          --include-unreferenced  also copy pictures on disk that the database does not reference
          --folder <path>         Cloudinary folder / public-id prefix (default vnews/articles; must match Cloudinary:Folder of the site)
          --transform <t>         transformation used in the delivery URL that verify opens (default f_auto,q_auto)
          --limit N               upload at most N new pictures this run (try 3 first)
          --yes                   required by upload
        Credentials: set CLOUDINARY_URL in your own terminal (never paste it anywhere).
        """;

    // ---------- plan ----------

    private static async Task<Plan> BuildPlanAsync(Options o)
    {
        var articles = Directory.Exists(Path.Combine(o.Uploads, "articles")) ? Path.Combine(o.Uploads, "articles") : o.Uploads;
        if (!Directory.Exists(articles)) throw new IOException($"Folder not found: {articles}");

        var plan = new Plan();
        var onDisk = new Dictionary<string, Local>(StringComparer.Ordinal);
        var all = new DirectoryInfo(articles).EnumerateFiles().ToList();
        plan.LocalFiles = all.Count;
        foreach (var file in all)
        {
            var match = FileNamePattern.Match(file.Name);
            if (!match.Success) { plan.IgnoredFiles++; continue; }
            onDisk[match.Groups["id"].Value] = new Local(match.Groups["id"].Value, match.Groups["ext"].Value, file.FullName, file.Length, "");
        }

        var connection = Environment.GetEnvironmentVariable(o.DbEnv);
        HashSet<string>? referenced = null;
        var referencedExt = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(connection))
        {
            plan.FromDatabase = true;
            referenced = new HashSet<string>(StringComparer.Ordinal);
            await foreach (var (value, source) in ReadReferencesAsync(connection))
            {
                foreach (Match m in ReferencePattern.Matches(value))
                {
                    var id = m.Groups["id"].Value;
                    referenced.Add(id);
                    referencedExt[id] = m.Groups["ext"].Value;
                    plan.Source[id] = plan.Source.TryGetValue(id, out var old) && !old.Contains(source) ? old + "+" + source : source;
                }
            }
        }

        var wanted = referenced is null ? onDisk.Keys.ToList() : referenced.ToList();
        foreach (var id in wanted.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!onDisk.TryGetValue(id, out var local)) { plan.MissingLocally.Add(id); continue; }
            if (referencedExt.TryGetValue(id, out var ext) && ext != local.Ext) { plan.ExtensionMismatch.Add($"{id} (database says .{ext}, disk has .{local.Ext})"); continue; }
            plan.Expected.Add(local with { Sha256 = Hash(local.Path) });
        }

        if (referenced is not null)
        {
            var extra = onDisk.Keys.Where(k => !referenced.Contains(k)).OrderBy(x => x, StringComparer.Ordinal).ToList();
            plan.Unreferenced.AddRange(extra);
            if (o.IncludeUnreferenced)
            {
                foreach (var id in extra) plan.Expected.Add(onDisk[id] with { Sha256 = Hash(onDisk[id].Path) });
            }
        }

        return plan;
    }

    /// <summary>Read-only: three SELECTs over the places a picture address can be stored.</summary>
    private static async IAsyncEnumerable<(string Value, string Source)> ReadReferencesAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        var queries = new (string Sql, string Source)[]
        {
            ("SELECT CoverImageUrl FROM Articles WHERE CoverImageUrl LIKE '%/uploads/articles/%'", "cover"),
            ("SELECT Url FROM ArticleImages WHERE Url LIKE '%/uploads/articles/%'", "gallery"),
            ("SELECT Body FROM Articles WHERE Body LIKE '%/uploads/articles/%'", "body"),
        };
        foreach (var (sql, source) in queries)
        {
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0)) yield return (reader.GetString(0), source);
            }
        }
    }

    private static void WritePlan(Options o, Plan plan)
    {
        var csv = new StringBuilder("id,ext,bytes,sha256,used_in\n");
        foreach (var f in plan.Expected) csv.Append($"{f.Id},{f.Ext},{f.Bytes},{f.Sha256},{(plan.Source.TryGetValue(f.Id, out var s) ? s : "")}\n");
        File.WriteAllText(Path.Combine(o.Report, "plan.csv"), csv.ToString());

        var problems = new StringBuilder();
        foreach (var id in plan.MissingLocally) problems.AppendLine($"MISSING ON DISK: {id}");
        foreach (var x in plan.ExtensionMismatch) problems.AppendLine($"EXTENSION MISMATCH: {x}");
        foreach (var id in plan.Unreferenced) problems.AppendLine($"UNREFERENCED (not copied unless --include-unreferenced): {id}");
        File.WriteAllText(Path.Combine(o.Report, "problems.txt"), problems.ToString());
    }

    private static void PrintPlan(Options o, Plan plan)
    {
        var total = plan.Expected.Sum(f => f.Bytes);
        var duplicates = plan.Expected.GroupBy(f => f.Sha256).Count(g => g.Count() > 1);
        var tooBig = plan.Expected.Count(f => f.Bytes > CloudinaryFreeImageLimit);
        Console.WriteLine($"Source          : {(plan.FromDatabase ? "database references + disk" : "disk only (set " + o.DbEnv + " to check against the database)")}");
        Console.WriteLine($"Files on disk   : {plan.LocalFiles} ({plan.IgnoredFiles} not in the generated-name shape, ignored)");
        Console.WriteLine($"To be on Cloudinary: {plan.Expected.Count} pictures, {total / 1048576.0:N1} MB, largest {(plan.Expected.Count == 0 ? 0 : plan.Expected.Max(f => f.Bytes)) / 1048576.0:N2} MB (Free limit 10 MB; over it: {tooBig})");
        Console.WriteLine($"Missing on disk : {plan.MissingLocally.Count}");
        Console.WriteLine($"Ext mismatches  : {plan.ExtensionMismatch.Count}");
        Console.WriteLine($"Unreferenced    : {plan.Unreferenced.Count}{(plan.FromDatabase ? "" : " (unknown without the database)")}");
        Console.WriteLine($"Identical-content groups (different names, same bytes): {duplicates}");
        Console.WriteLine($"Report folder   : {Path.GetFullPath(o.Report)}");
    }

    // ---------- upload ----------

    private static async Task<int> UploadAsync(Options o, Plan plan)
    {
        if (!o.Yes) { Console.Error.WriteLine("\nupload needs --yes (nothing was sent). Run plan first and read the numbers above."); return 2; }
        var (cloudinary, cloud) = Connect();
        Console.WriteLine($"\nCloudinary account: {cloud}  folder: {o.Folder}");

        var existing = await ListExistingAsync(cloudinary, o.Folder);
        Console.WriteLine($"Already on Cloudinary under {o.Folder}/: {existing.Count}");

        var state = Path.Combine(o.Report, "state.jsonl");
        int uploaded = 0, already = 0, conflicts = 0, failed = 0, skippedByLimit = 0;
        foreach (var file in plan.Expected)
        {
            var publicId = $"{o.Folder}/{file.Id}";
            if (existing.TryGetValue(publicId, out var bytes))
            {
                if (bytes == file.Bytes) { already++; Log(state, file, "already", null); }
                else { conflicts++; Log(state, file, "conflict", $"Cloudinary has {bytes} bytes, disk has {file.Bytes}"); Console.Error.WriteLine($"CONFLICT (not overwritten): {file.Id}"); }
                continue;
            }

            if (uploaded + failed >= o.Limit) { skippedByLimit++; continue; }

            var error = await UploadOneAsync(cloudinary, file, publicId);
            if (error is null) { uploaded++; Log(state, file, "uploaded", null); if (uploaded % 20 == 0) Console.WriteLine($"  uploaded {uploaded}..."); }
            else { failed++; Log(state, file, "failed", error); Console.Error.WriteLine($"FAILED {file.Id}: {error}"); }
            await Task.Delay(100);
        }

        Console.WriteLine($"\nUploaded now: {uploaded}   Already there: {already}   Conflicts: {conflicts}   Failed: {failed}   Left for another run (limit): {skippedByLimit}");
        Console.WriteLine(failed + conflicts == 0 ? "Run 'verify' next." : "Run 'upload' again to retry the failed ones; conflicts need a look (nothing was overwritten).");
        return failed + conflicts == 0 ? 0 : 1;
    }

    private static async Task<string?> UploadOneAsync(Cloudinary cloudinary, Local file, string publicId)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await using var stream = File.OpenRead(file.Path);
                var result = await cloudinary.UploadAsync(new ImageUploadParams
                {
                    File = new FileDescription(Path.GetFileName(file.Path), stream),
                    PublicId = publicId,
                    Overwrite = false,
                    UniqueFilename = false,
                    UseFilename = false,
                });
                if (result.Error is null && (int)result.StatusCode < 400) return null;
                var message = result.Error?.Message ?? $"HTTP {(int)result.StatusCode}";
                if (attempt == 3 || (int)result.StatusCode is >= 400 and < 500 and not 429) return message; // a refusal will not change by retrying
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                if (attempt == 3) return ex.Message;
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
            }
        }

        return "unknown error";
    }

    private static void Log(string path, Local file, string status, string? error) =>
        File.AppendAllText(path, JsonSerializer.Serialize(new { utc = DateTime.UtcNow, file.Id, file.Ext, file.Bytes, file.Sha256, status, error }) + "\n");

    // ---------- verify ----------

    private static async Task<int> VerifyAsync(Options o, Plan plan)
    {
        var (cloudinary, cloud) = Connect();
        Console.WriteLine($"\nCloudinary account: {cloud}  folder: {o.Folder}");
        var existing = await ListExistingAsync(cloudinary, o.Folder);

        var expectedIds = plan.Expected.Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
        var missing = plan.Expected.Where(f => !existing.ContainsKey($"{o.Folder}/{f.Id}")).ToList();
        var wrongSize = plan.Expected.Where(f => existing.TryGetValue($"{o.Folder}/{f.Id}", out var b) && b != f.Bytes).ToList();
        var extra = existing.Keys.Where(k => !expectedIds.Contains(k[(o.Folder.Length + 1)..])).OrderBy(x => x, StringComparer.Ordinal).ToList();

        // Open the exact URL the site will print for every picture that is there.
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Accept.ParseAdd("image/avif,image/webp,image/*,*/*;q=0.8");
        var middle = o.Transform.Length == 0 ? "" : o.Transform + "/";
        var failures = new List<string>();
        var ok = 0;
        using var gate = new SemaphoreSlim(4);
        await Task.WhenAll(plan.Expected.Where(f => existing.ContainsKey($"{o.Folder}/{f.Id}")).Select(async f =>
        {
            await gate.WaitAsync();
            try
            {
                var url = $"https://res.cloudinary.com/{cloud}/image/upload/{middle}{o.Folder}/{f.Id}";
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                var type = response.Content.Headers.ContentType?.MediaType ?? "";
                if (response.IsSuccessStatusCode && type.StartsWith("image/", StringComparison.Ordinal)) Interlocked.Increment(ref ok);
                else lock (failures) failures.Add($"{f.Id}: HTTP {(int)response.StatusCode} {type}");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lock (failures) failures.Add($"{f.Id}: {ex.GetType().Name}");
            }
            finally { gate.Release(); }
        }));

        var report = new StringBuilder();
        foreach (var f in missing) report.AppendLine($"MISSING ON CLOUDINARY: {f.Id}");
        foreach (var f in wrongSize) report.AppendLine($"SIZE DIFFERS: {f.Id}");
        foreach (var x in extra) report.AppendLine($"EXTRA ON CLOUDINARY (not in plan): {x}");
        foreach (var x in failures.OrderBy(x => x, StringComparer.Ordinal)) report.AppendLine($"NOT DISPLAYABLE: {x}");
        File.WriteAllText(Path.Combine(o.Report, "verify.txt"), report.ToString());

        Console.WriteLine($"Expected: {plan.Expected.Count}   Found on Cloudinary: {plan.Expected.Count - missing.Count}   Missing: {missing.Count}   Size differs: {wrongSize.Count}");
        Console.WriteLine($"Delivery URLs that open as an image: {ok} of {plan.Expected.Count - missing.Count}   Failing: {failures.Count}   Extra on Cloudinary: {extra.Count}");
        var good = missing.Count + wrongSize.Count + failures.Count == 0;
        Console.WriteLine(good ? "RESULT: every expected picture is on Cloudinary with the same size and opens as an image." : "RESULT: problems found, see verify.txt.");
        return good ? 0 : 1;
    }

    // ---------- Cloudinary ----------

    private static (Cloudinary Client, string Cloud) Connect()
    {
        var match = UrlPattern.Match((Environment.GetEnvironmentVariable("CLOUDINARY_URL") ?? "").Trim());
        if (!match.Success) throw new InvalidOperationException("Set the CLOUDINARY_URL environment variable in this terminal first (cloudinary://API_KEY:API_SECRET@CLOUD_NAME).");
        var cloud = match.Groups["cloud"].Value;
        return (new Cloudinary(new Account(cloud, match.Groups["key"].Value, match.Groups["secret"].Value)) { Api = { Secure = true } }, cloud);
    }

    /// <summary>public id -> size in bytes, for every image under the folder (pages of 500; one or two calls for this site).</summary>
    private static async Task<Dictionary<string, long>> ListExistingAsync(Cloudinary cloudinary, string folder)
    {
        var found = new Dictionary<string, long>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            var result = await cloudinary.ListResourcesAsync(new ListResourcesByPrefixParams
            {
                Prefix = folder + "/",
                Type = "upload",
                MaxResults = 500,
                NextCursor = cursor,
            });
            if (result.Error is not null) throw new InvalidOperationException($"Cloudinary list failed: {result.Error.Message}");
            foreach (var r in result.Resources) found[r.PublicId] = r.Bytes;
            cursor = result.NextCursor;
        } while (!string.IsNullOrEmpty(cursor));

        return found;
    }

    // ---------- helpers ----------

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static bool TryParse(string[] args, out Options o, out string error)
    {
        o = new Options();
        error = "";
        if (args.Length == 0 || args[0] is not ("plan" or "upload" or "verify")) { error = "First argument must be plan, upload or verify."; return false; }
        o.Command = args[0];
        for (var i = 1; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
            try
            {
                switch (args[i])
                {
                    case "--uploads": o.Uploads = Next(); break;
                    case "--report": o.Report = Next(); break;
                    case "--folder": o.Folder = Next().Trim('/'); break;
                    case "--transform": o.Transform = Next(); break;
                    case "--db-env": o.DbEnv = Next(); break;
                    case "--limit": o.Limit = int.Parse(Next()); break;
                    case "--yes": o.Yes = true; break;
                    case "--include-unreferenced": o.IncludeUnreferenced = true; break;
                    default: error = $"Unknown option {args[i]}."; return false;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException) { error = ex.Message; return false; }
        }

        if (string.IsNullOrWhiteSpace(o.Uploads)) { error = "--uploads is required."; return false; }
        if (!Regex.IsMatch(o.Folder, "^[A-Za-z0-9_-]+(/[A-Za-z0-9_-]+)*$")) { error = "--folder has characters Cloudinary public ids should not use."; return false; }
        return true;
    }
}
