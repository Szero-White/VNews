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
