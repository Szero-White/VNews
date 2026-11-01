using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Domain.Entities;
using VNews.Domain.Identity;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;
using VNews.Web.Demo;

namespace VNews.Tests.Integration;

/// <summary>One-click demo sign-in, with the switch ON (Development, DemoAuth:Enabled=true, isolated test database).</summary>
public class DemoAuthIntegrationTests : IClassFixture<DemoAuthWebApplicationFactory>
{
    private const string DemoPath = "/tai-khoan/demo/dang-nhap";
    private readonly DemoAuthWebApplicationFactory _factory;

    public DemoAuthIntegrationTests(DemoAuthWebApplicationFactory factory) => _factory = factory;

    internal static async Task<HttpResponseMessage> PostDemoAsync(HttpClient client, string role, bool withToken = true, Action<HttpRequestMessage>? configure = null,
        Dictionary<string, string>? extraFields = null, string tokenPage = "/tai-khoan/dang-nhap")
    {
        // A signed-in user is redirected away from the login page, so switching persona takes its token from the profile page.
        var page = await client.GetAsync(tokenPage);
        var fields = new Dictionary<string, string> { ["role"] = role };
        if (withToken) fields["__RequestVerificationToken"] = await HttpHelpers.TokenFromAsync(page);
        foreach (var pair in extraFields ?? new()) fields[pair.Key] = pair.Value;
        var request = new HttpRequestMessage(HttpMethod.Post, DemoPath) { Content = new FormUrlEncodedContent(fields) };
        configure?.Invoke(request);
        return await client.SendAsync(request);
    }

    private static string Location(HttpResponseMessage response) => response.Headers.Location!.OriginalString;

    private static void AssertChallenged(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/tai-khoan/dang-nhap", Location(response));
    }

    private static void AssertAccessDenied(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/tai-khoan/khong-co-quyen", Location(response));
    }

    // ---------- what the page offers ----------

    [Fact]
    public async Task LoginPage_OffersOneCardPerRealRole_AsPostFormsWithTokens_AndExposesNoSecrets()
    {
        var html = WebUtility.HtmlDecode(await _factory.NewClient().GetStringAsync("/tai-khoan/dang-nhap"));

        Assert.Contains("Trải nghiệm sản phẩm", html);
        var forms = Regex.Matches(html, "<form method=\"post\" class=\"demo-card[^\"]*\" action=\"" + DemoPath + "\">[\\s\\S]*?</form>").Select(m => m.Value).ToList();
        Assert.Equal(4, forms.Count);
        foreach (var key in new[] { "reader", "author", "editor", "admin" })
        {
            Assert.Single(forms, f => f.Contains($"name=\"role\" value=\"{key}\""));
        }
        Assert.All(forms, f => Assert.Contains("name=\"__RequestVerificationToken\"", f));
        foreach (var title in new[] { "Độc giả", "Tác giả", "Biên tập viên", "Quản trị viên" })
        {
            Assert.Contains($"<h3 class=\"demo-card__title\">{title}</h3>", html);
            Assert.Contains($"aria-label=\"Vào demo {title}\"", html); // the buttons differ only by their accessible name
        }
        Assert.Equal(4, Regex.Matches(html, ">Vào demo</button>").Count); // identical, short visible text: nothing wraps

        // No password, account address, token or role id in the markup, and no demo JavaScript at all.
        Assert.DoesNotContain("Demo@12345", html);
        Assert.DoesNotContain("demo.vnews.local", html);
        Assert.DoesNotContain("password=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"<script[^>]*>[^<]*demo", html);
    }

    [Fact]
    public async Task SignInDialog_OffersTheSameCards_OnPublicPages()
    {
        var html = await _factory.NewClient().GetStringAsync("/");
        Assert.Contains("demo-login--dialog", html);
        Assert.Equal(4, Regex.Matches(html, "data-demo-role=\"").Count);
    }

    // ---------- each role ----------

    [Fact]
    public async Task ReaderDemo_SignsIn_SeesTheDemoBadge_AndCanSaveAnArticle()
    {
        var client = _factory.NewClient();
        var response = await PostDemoAsync(client, "reader");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", Location(response));
        Assert.True(HttpHelpers.SetsAuthCookie(response));

        var home = WebUtility.HtmlDecode(await client.GetStringAsync("/"));
        Assert.Contains("class=\"demo-badge\"", home);
        Assert.Contains("DEMO</strong> · Độc giả", home);

        // Saving an article works through the normal authenticated bookmark API.
        int id; string slug;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
            (id, slug) = await db.Articles.Where(a => a.Status == ArticleStatus.Published).OrderBy(a => a.Id).Select(a => new ValueTuple<int, string>(a.Id, a.Slug)).FirstAsync();
        }
        var article = await client.GetStringAsync($"/tin/{slug}");
        var token = Regex.Match(article, "name=\"vnews-csrf-token\" content=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));

        var save = new HttpRequestMessage(HttpMethod.Post, $"/api/me/bookmarks/{id}");
        save.Headers.Add("RequestVerificationToken", token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(save)).StatusCode);
        var saved = await client.GetFromJsonAsync<JsonElement>("/api/me/bookmarks");
        Assert.Contains(saved.EnumerateArray(), item => item.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task ReaderDemo_IsRefusedByEveryNewsroomArea()
    {
        var client = _factory.NewClient();
        await PostDemoAsync(client, "reader");

        foreach (var path in new[] { "/cms", "/cms/CmsArticles/Create", "/cms/CmsUsers", "/cms/CmsCategories" })
        {
            AssertAccessDenied(await client.GetAsync(path));
        }
    }

    [Fact]
    public async Task AuthorDemo_LandsInTheNewsroom_AndCreatesADraftOwnedByTheDemoAuthor()
    {
        var client = _factory.NewClient();
        var response = await PostDemoAsync(client, "author");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/cms", Location(response));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Location(response))).StatusCode);

        int categoryId;
        string demoAuthorId;
        using (var scope = _factory.Services.CreateScope())
        {
            categoryId = await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Categories.OrderBy(c => c.Id).Select(c => c.Id).FirstAsync();
            demoAuthorId = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("demo.author@demo.vnews.local"))!.Id;
        }

        var title = $"Bản nháp demo {Guid.NewGuid():N}";
        var create = await client.PostFormAsync("/cms/CmsArticles/Create", "/cms/CmsArticles/Create", new Dictionary<string, string>
        {
            ["Title"] = title, ["Summary"] = "Tóm tắt bản nháp do tác giả demo tạo ra.", ["Body"] = "<p>Nội dung bản nháp của tác giả demo.</p>", ["CategoryId"] = categoryId.ToString(),
        });
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);

        using var verify = _factory.Services.CreateScope();
        var draft = await verify.ServiceProvider.GetRequiredService<VNewsDbContext>().Articles.AsNoTracking().SingleAsync(a => a.Title == title);
        Assert.Equal(ArticleStatus.Draft, draft.Status);
        Assert.Equal(demoAuthorId, draft.AuthorUserId);

        // Not an editor or admin: no publishing, no user or category management.
        AssertAccessDenied(await client.GetAsync("/cms/CmsUsers"));
        AssertAccessDenied(await client.GetAsync("/cms/CmsCategories"));
    }

    [Fact]
    public async Task EditorDemo_LandsInTheReviewQueue_WithEditorOnlyPowers()
    {
        var client = _factory.NewClient();
        var response = await PostDemoAsync(client, "editor");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("status=PendingReview", Location(response));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Location(response))).StatusCode);

        // Editors may act on the review workflow (a missing id is a 404, not a 403)...
        var publish = await client.PostFormAsync("/cms/CmsArticles/Publish/987654", "/cms", new());
        Assert.Equal(HttpStatusCode.NotFound, publish.StatusCode);
        // ...but not manage users or categories.
        AssertAccessDenied(await client.GetAsync("/cms/CmsUsers"));
        AssertAccessDenied(await client.GetAsync("/cms/CmsCategories"));
    }

    [Fact]
    public async Task AdminDemo_LandsOnTheSystemOverview_AndCanOpenEveryNewsroomArea()
    {
        var client = _factory.NewClient();
        var response = await PostDemoAsync(client, "admin");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/CmsOverview", Location(response));

        foreach (var path in new[] { Location(response), "/cms/CmsCategories", "/cms/CmsArticles", "/cms/CmsArticles/Create" })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        }
        var cms = await client.GetStringAsync("/cms/CmsOverview");
        Assert.Contains("class=\"demo-badge\"", cms);
        Assert.Contains("DEMO</strong> · Quản trị viên", WebUtility.HtmlDecode(cms));
    }

    [Fact]
    public async Task SwitchingRole_ReplacesTheSessionInsteadOfStackingPrivileges()
    {
        var client = _factory.NewClient();
        await PostDemoAsync(client, "admin");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/cms/CmsUsers")).StatusCode);

        await PostDemoAsync(client, "reader", tokenPage: "/tai-khoan/ho-so");
        AssertAccessDenied(await client.GetAsync("/cms/CmsUsers"));
        AssertAccessDenied(await client.GetAsync("/cms"));
    }

    // ---------- the client cannot steer the server ----------

    [Theory]
    [InlineData("")]
    [InlineData("superuser")]
    [InlineData("Admin; Reader")]
    [InlineData("../admin")]
    [InlineData("demo.admin@demo.vnews.local")]
    [InlineData("admin@vnews.local")]
    public async Task UnknownRoleKeys_AreRejected_AndNeverSignAnyoneIn(string role)
    {
        var client = _factory.NewClient();
        var response = await PostDemoAsync(client, role);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(HttpHelpers.SetsAuthCookie(response));
    }

    [Fact]
    public async Task ExtraFieldsInTheRequest_CannotChangeTheAccountOrRole()
    {
        var client = _factory.NewClient();
        var response = await PostDemoAsync(client, "reader", extraFields: new()
        {
            ["Role"] = "Admin", ["roles"] = "Admin", ["userId"] = "1", ["email"] = "admin@vnews.local", ["returnUrl"] = "/cms/CmsUsers",
        });

        Assert.Equal("/", Location(response)); // a reader's landing page, not the injected URL
        AssertAccessDenied(await client.GetAsync("/cms/CmsUsers"));
    }

    [Fact]
    public async Task MissingOrWrongAntiForgeryToken_IsRejected()
    {
        var client = _factory.NewClient();

        var missing = await PostDemoAsync(client, "admin", withToken: false);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.False(HttpHelpers.SetsAuthCookie(missing));

        var page = await client.GetAsync("/tai-khoan/dang-nhap");
        _ = page;
        var wrong = await client.PostAsync(DemoPath, new FormUrlEncodedContent(new Dictionary<string, string> { ["role"] = "admin", ["__RequestVerificationToken"] = "not-a-real-token" }));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.False(HttpHelpers.SetsAuthCookie(wrong));
        AssertChallenged(await client.GetAsync("/cms")); // still anonymous
    }

    [Fact]
    public async Task OnlyPostIsAccepted_NoPasswordOrTokenInTheUrl()
    {
        var client = _factory.NewClient();
        foreach (var url in new[] { DemoPath + "?role=admin", DemoPath })
        {
            var response = await client.GetAsync(url);
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"GET {url} returned {(int)response.StatusCode}");
            Assert.False(HttpHelpers.SetsAuthCookie(response));
        }
    }

    // ---------- environment limits ----------

    [Fact]
    public async Task RequestsThatDoNotComeFromThisMachine_AreRefused()
    {
        var client = _factory.NewClient();

        var remote = await PostDemoAsync(client, "admin", configure: r => r.Headers.Add("X-Test-Remote-IP", "203.0.113.9"));
        var proxied = await PostDemoAsync(client, "admin", configure: r => r.Headers.Add("X-Forwarded-For", "203.0.113.9"));
        var otherHost = await PostDemoAsync(client, "admin", configure: r => r.Headers.Host = "demo.example.com");

        foreach (var response in new[] { remote, proxied, otherHost })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.False(HttpHelpers.SetsAuthCookie(response));
        }
        AssertChallenged(await client.GetAsync("/cms"));
    }

    [Fact]
    public async Task TheDemoOffer_IsNotRenderedForARemoteRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/tai-khoan/dang-nhap");
        request.Headers.Add("X-Test-Remote-IP", "198.51.100.20");
        var html = await (await _factory.NewClient().SendAsync(request)).Content.ReadAsStringAsync();

        Assert.DoesNotContain("Trải nghiệm sản phẩm", html);
        Assert.DoesNotContain("demo-card", html);
    }

    // ---------- demo accounts ----------

    [Fact]
    public async Task DemoAccounts_AreSeparateFromRealUsers_HaveExactlyTheirRole_AndNoUsablePassword()
    {
        foreach (var key in new[] { "reader", "author", "editor", "admin" }) await PostDemoAsync(_factory.NewClient(), key);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var persona in DemoPersonas.All)
        {
            var user = (await users.FindByEmailAsync(persona.Email))!;
            Assert.EndsWith("@demo.vnews.local", user.Email);
            Assert.Equal(new[] { persona.Role }, await users.GetRolesAsync(user));
            Assert.True(await users.HasPasswordAsync(user));
            foreach (var guess in new[] { "Demo@12345", "Password1!", "Demo12345!", persona.Key })
            {
                Assert.False(await users.CheckPasswordAsync(user, guess), $"{persona.Email} must not accept '{guess}'");
            }
        }

        // The long-standing seeded staff accounts are untouched and still separate.
        Assert.NotNull(await users.FindByEmailAsync("author@vnews.local"));
        Assert.Equal(4, users.Users.Count(u => u.Email!.EndsWith("@demo.vnews.local")));
    }

    [Fact]
    public async Task DemoAccounts_CannotBeSignedInWithAPasswordThroughTheNormalForm()
    {
        var client = _factory.NewClient();
        await PostDemoAsync(client, "admin");

        var anonymous = _factory.NewClient();
        var attempt = await anonymous.PostFormAsync("/tai-khoan/dang-nhap", "/tai-khoan/dang-nhap",
            new() { ["Email"] = "demo.admin@demo.vnews.local", ["Password"] = "Demo@12345" });

        Assert.Equal(HttpStatusCode.OK, attempt.StatusCode); // form redisplayed with an error
        Assert.False(HttpHelpers.SetsAuthCookie(attempt));
        AssertChallenged(await anonymous.GetAsync("/cms"));
    }

    [Fact]
    public async Task DemoAccount_IsRepaired_WhenItsRoleWasChangedOrItWasLockedOut()
    {
        var client = _factory.NewClient();
        await PostDemoAsync(client, "reader");
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var reader = (await users.FindByEmailAsync("demo.reader@demo.vnews.local"))!;
            await users.AddToRoleAsync(reader, Roles.Admin); // tampering
            await users.SetLockoutEndDateAsync(reader, DateTimeOffset.UtcNow.AddHours(1));
        }

        var fresh = _factory.NewClient();
        var response = await PostDemoAsync(fresh, "reader");

        Assert.Equal("/", Location(response));
        AssertAccessDenied(await fresh.GetAsync("/cms/CmsUsers")); // the extra Admin role was removed again
    }

    // ---------- sign-out ----------

    [Fact]
    public async Task SigningOut_EndsTheDemoSession_AndProtectedPagesAskForSignInAgain()
    {
        var client = _factory.NewClient();
        await PostDemoAsync(client, "admin");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/cms/CmsUsers")).StatusCode);

        var logout = await client.PostFormAsync("/tai-khoan/dang-xuat", "/cms/CmsUsers", new());
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

        var after = await client.GetAsync("/cms/CmsUsers");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Contains("/tai-khoan/dang-nhap", Location(after));
        Assert.DoesNotContain("demo-badge", await client.GetStringAsync("/"));
    }
}

/// <summary>Unified sign-in: roles decide where people land, return URLs are validated, old CMS address still works.</summary>
public class UnifiedSignInIntegrationTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public UnifiedSignInIntegrationTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private static string Location(HttpResponseMessage response) => response.Headers.Location!.OriginalString;

    private static Task<HttpResponseMessage> Login(HttpClient client, string email, string password, string? returnUrl = null)
    {
        var fields = new Dictionary<string, string> { ["Email"] = email, ["Password"] = password };
        if (returnUrl is not null) fields["ReturnUrl"] = returnUrl;
        return client.PostFormAsync("/tai-khoan/dang-nhap", "/tai-khoan/dang-nhap", fields);
    }

    private async Task<string> RegisterReaderAsync()
    {
        var email = $"reader-{Guid.NewGuid():N}@example.test";
        var response = await _factory.NewClient().PostFormAsync("/tai-khoan/dang-ky", "/tai-khoan/dang-ky", new()
        {
            ["DisplayName"] = "Reader", ["Email"] = email, ["Password"] = "TestPassword123!", ["ConfirmPassword"] = "TestPassword123!",
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return email;
    }

    [Theory]
    [InlineData("author@vnews.local", "/cms")]
    [InlineData("editor@vnews.local", "status=PendingReview")]
    [InlineData("admin@vnews.local", "/CmsOverview")]
    public async Task StaffSignIn_GoesToTheirOwnAreaOfTheNewsroom(string email, string expectedPart)
    {
        var client = _factory.NewClient();
        var response = await Login(client, email, "Demo@12345");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(expectedPart, Location(response));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Location(response))).StatusCode);
    }

    [Fact]
    public async Task ReaderSignIn_GoesToThePublicSite()
    {
        var email = await RegisterReaderAsync();
        var response = await Login(_factory.NewClient(), email, "TestPassword123!");
        Assert.Equal("/", Location(response));
    }

    [Theory]
    [InlineData("https://evil.example/phish")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://localhost.evil.example/")]
    public async Task ExternalReturnUrls_NeverRedirectOffSite_ForReadersOrStaff(string returnUrl)
    {
        var readerEmail = await RegisterReaderAsync();
        var reader = await Login(_factory.NewClient(), readerEmail, "TestPassword123!", returnUrl);
        Assert.Equal("/", Location(reader));

        var staff = await Login(_factory.NewClient(), "author@vnews.local", "Demo@12345", returnUrl);
        Assert.StartsWith("/cms", Location(staff));
        Assert.DoesNotContain("evil", Location(staff));
    }

    [Fact]
    public async Task LocalReturnUrl_IsHonoured_ForPublicPages()
    {
        var email = await RegisterReaderAsync();
        var response = await Login(_factory.NewClient(), email, "TestPassword123!", "/chuyen-muc/cong-nghe");
        Assert.Equal("/chuyen-muc/cong-nghe", Location(response));
    }

    [Fact]
    public async Task AlreadySignedIn_VisitingTheLoginPage_IsForwardedLocallyAccordingToRole()
    {
        var client = _factory.NewClient();
        await Login(client, "editor@vnews.local", "Demo@12345");

        var page = await client.GetAsync("/tai-khoan/dang-nhap?returnUrl=" + Uri.EscapeDataString("https://evil.example"));
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.StartsWith("/cms", Location(page));
    }

    [Fact]
    public async Task LegacyCmsLoginAddress_ForwardsToTheSharedPage_WithAValidatedReturnUrl()
    {
        var client = _factory.NewClient();

        var plain = await client.GetAsync("/cms/dang-nhap");
        Assert.Equal(HttpStatusCode.Redirect, plain.StatusCode);
        Assert.Equal("/tai-khoan/dang-nhap?returnUrl=%2Fcms", Location(plain));

        var deep = await client.GetAsync("/cms/dang-nhap?returnUrl=" + Uri.EscapeDataString("/cms/CmsArticles/Create"));
        Assert.Equal("/tai-khoan/dang-nhap?returnUrl=%2Fcms%2FCmsArticles%2FCreate", Location(deep));

        foreach (var bad in new[] { "https://evil.example", "//evil.example", "/tin-moi-nhat" })
        {
            var forwarded = await client.GetAsync("/cms/dang-nhap?returnUrl=" + Uri.EscapeDataString(bad));
            Assert.Equal("/tai-khoan/dang-nhap?returnUrl=%2Fcms", Location(forwarded)); // never an outside or non-CMS target
        }

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsync("/cms/dang-nhap", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
    }

    [Fact]
    public async Task AnonymousVisitsToTheCms_GoToTheSharedSignIn_AndComeBackAfterwards()
    {
        var client = _factory.NewClient();
        var challenged = await client.GetAsync("/cms/CmsArticles/Create");
        Assert.Equal(HttpStatusCode.Redirect, challenged.StatusCode);
        Assert.Contains("/tai-khoan/dang-nhap", Location(challenged));

        var back = await client.PostFormAsync("/tai-khoan/dang-nhap", Location(challenged), new() { ["Email"] = "author@vnews.local", ["Password"] = "Demo@12345", ["ReturnUrl"] = "/cms/CmsArticles/Create" });
        Assert.Equal("/cms/CmsArticles/Create", Location(back));
    }

    [Fact]
    public async Task WrongPassword_ShowsAnError_AndDoesNotSignIn()
    {
        var client = _factory.NewClient();
        var response = await Login(client, "author@vnews.local", "wrong-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Email hoặc mật khẩu không đúng", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.False(HttpHelpers.SetsAuthCookie(response));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/cms")).StatusCode);
    }

    // ---------- the sign-in dialog (same endpoint, JSON answers, stays on the page) ----------

    private static async Task<HttpResponseMessage> DialogLogin(HttpClient client, Dictionary<string, string> fields)
    {
        var page = await client.GetAsync("/tai-khoan/dang-nhap");
        fields["__RequestVerificationToken"] = await HttpHelpers.TokenFromAsync(page);
        var request = new HttpRequestMessage(HttpMethod.Post, "/tai-khoan/dang-nhap") { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Add("X-Requested-With", "fetch");
        request.Headers.Add("Accept", "application/json");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Dialog_WrongPassword_Returns401Json_WithTheMessage_AndNoCookie()
    {
        var response = await DialogLogin(_factory.NewClient(), new() { ["Email"] = "author@vnews.local", ["Password"] = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("Email hoặc mật khẩu không đúng", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.False(HttpHelpers.SetsAuthCookie(response));
    }

    [Fact]
    public async Task Dialog_InvalidInput_Returns400Json()
    {
        var response = await DialogLogin(_factory.NewClient(), new() { ["Email"] = "not-an-email", ["Password"] = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("error", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Dialog_Success_ReturnsALocalRedirectChosenByTheServer()
    {
        var response = await DialogLogin(_factory.NewClient(), new() { ["Email"] = "editor@vnews.local", ["Password"] = "Demo@12345", ["ReturnUrl"] = "https://evil.example" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(HttpHelpers.SetsAuthCookie(response));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var redirect = json.RootElement.GetProperty("redirect").GetString()!;
        Assert.StartsWith("/cms", redirect);
        Assert.DoesNotContain("evil", redirect);
    }

    // ---------- registration ----------

    private static Task<HttpResponseMessage> Register(HttpClient client, string email, Dictionary<string, string>? extra = null)
    {
        var fields = new Dictionary<string, string> { ["DisplayName"] = "Người dùng thử", ["Email"] = email, ["Password"] = "TestPassword123!", ["ConfirmPassword"] = "TestPassword123!" };
        foreach (var pair in extra ?? new()) fields[pair.Key] = pair.Value;
        return client.PostFormAsync("/tai-khoan/dang-ky", "/tai-khoan/dang-ky", fields);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Editor")]
    [InlineData("Author")]
    public async Task Registration_AlwaysCreatesAReader_WhateverRoleTheClientSends(string injectedRole)
    {
        var email = $"new-{Guid.NewGuid():N}@example.test";
        var client = _factory.NewClient();

        var response = await Register(client, email, new() { ["Role"] = injectedRole, ["Roles"] = injectedRole, ["role"] = injectedRole });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", Location(response)); // a reader lands on the public site
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<VNews.Domain.Identity.ApplicationUser>>();
        Assert.Equal(new[] { "Reader" }, await users.GetRolesAsync((await users.FindByEmailAsync(email))!));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/cms/CmsUsers")).StatusCode); // denied
    }

    [Fact]
    public async Task Registration_RejectsDuplicateEmail_WeakPasswordAndMismatch_WithoutSigningIn()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.Redirect, (await Register(_factory.NewClient(), email)).StatusCode);

        var duplicate = await Register(_factory.NewClient(), email);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("Email này đã được sử dụng", WebUtility.HtmlDecode(await duplicate.Content.ReadAsStringAsync()));
        Assert.False(HttpHelpers.SetsAuthCookie(duplicate));

        var weak = await _factory.NewClient().PostFormAsync("/tai-khoan/dang-ky", "/tai-khoan/dang-ky",
            new() { ["DisplayName"] = "Yếu", ["Email"] = $"weak-{Guid.NewGuid():N}@example.test", ["Password"] = "abc", ["ConfirmPassword"] = "abc" });
        Assert.Equal(HttpStatusCode.OK, weak.StatusCode);
        Assert.False(HttpHelpers.SetsAuthCookie(weak));

        var mismatch = await _factory.NewClient().PostFormAsync("/tai-khoan/dang-ky", "/tai-khoan/dang-ky",
            new() { ["DisplayName"] = "Lệch", ["Email"] = $"mm-{Guid.NewGuid():N}@example.test", ["Password"] = "TestPassword123!", ["ConfirmPassword"] = "TestPassword123?" });
        Assert.Equal(HttpStatusCode.OK, mismatch.StatusCode);
        Assert.False(HttpHelpers.SetsAuthCookie(mismatch));
    }

    [Fact]
    public async Task Registration_ValidatesTheReturnUrl_NoOpenRedirect()
    {
        var local = await Register(_factory.NewClient(), $"ru1-{Guid.NewGuid():N}@example.test", new() { ["ReturnUrl"] = "/chuyen-muc/cong-nghe" });
        Assert.Equal("/chuyen-muc/cong-nghe", Location(local));

        foreach (var bad in new[] { "https://evil.example", "//evil.example", "/\\evil.example" })
        {
            var response = await Register(_factory.NewClient(), $"ru-{Guid.NewGuid():N}@example.test", new() { ["ReturnUrl"] = bad });
            Assert.Equal("/", Location(response));
        }
    }

    [Fact]
    public async Task Registration_RequiresTheAntiForgeryToken()
    {
        var client = _factory.NewClient();
        var response = await client.PostAsync("/tai-khoan/dang-ky", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["DisplayName"] = "Không token", ["Email"] = $"nt-{Guid.NewGuid():N}@example.test", ["Password"] = "TestPassword123!", ["ConfirmPassword"] = "TestPassword123!",
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(HttpHelpers.SetsAuthCookie(response));
    }

    [Fact]
    public async Task WithDemoOff_NoDemoOfferAppears_AndTheEndpointDoesNotExist()
    {
        var client = _factory.NewClient();
        var html = await client.GetStringAsync("/tai-khoan/dang-nhap");
        Assert.DoesNotContain("Trải nghiệm sản phẩm", html);
        Assert.DoesNotContain("demo-card", html);

        var response = await DemoAuthIntegrationTests.PostDemoAsync(client, "admin");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(HttpHelpers.SetsAuthCookie(response));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/cms")).StatusCode);
    }

    [Fact]
    public async Task WithDemoOff_EvenAnInvalidOrMissingToken_GetsTheSameNotFound()
    {
        var client = _factory.NewClient();
        var withoutToken = await DemoAuthIntegrationTests.PostDemoAsync(client, "admin", withToken: false);
        Assert.Equal(HttpStatusCode.NotFound, withoutToken.StatusCode); // the endpoint reveals nothing, not even "bad token"
    }
}

/// <summary>Production: refused even when the configuration wrongly says Enabled=true.</summary>
public class DemoAuthProductionIntegrationTests : IClassFixture<DemoAuthProductionWebApplicationFactory>
{
    private readonly DemoAuthProductionWebApplicationFactory _factory;

    public DemoAuthProductionIntegrationTests(DemoAuthProductionWebApplicationFactory factory) => _factory = factory;

    // Production forces Secure cookies, so requests must arrive over HTTPS (as behind TLS).
    private HttpClient Https() => _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    [Theory]
    [InlineData("reader")]
    [InlineData("author")]
    [InlineData("editor")]
    [InlineData("admin")]
    public async Task EveryRole_IsRefused_InProduction_EvenWithEnabledTrue(string role)
    {
        var client = Https();
        var response = await DemoAuthIntegrationTests.PostDemoAsync(client, role);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(HttpHelpers.SetsAuthCookie(response));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/cms")).StatusCode);
    }

    [Fact]
    public async Task NoDemoOffer_IsRendered_InProduction()
    {
        var client = Https();
        var html = await client.GetStringAsync("/tai-khoan/dang-nhap");
        Assert.DoesNotContain("Trải nghiệm sản phẩm", html);
        Assert.DoesNotContain("demo-card", html);
        Assert.DoesNotContain("data-demo-login", await client.GetStringAsync("/")); // the stylesheet is shared, the offer markup is not
    }

    [Fact]
    public async Task NoDemoAccounts_AreCreated_InProduction()
    {
        await DemoAuthIntegrationTests.PostDemoAsync(Https(), "admin");

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Empty(users.Users.Where(u => u.Email!.EndsWith("@demo.vnews.local")));
    }
}
