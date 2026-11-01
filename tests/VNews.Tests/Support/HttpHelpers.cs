using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VNews.Tests.Support;

public static class HttpHelpers
{
    private static readonly Regex TokenRegex = new(
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.Compiled);

    public static HttpClient NewClient(this WebApplicationFactory<Program> factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static async Task<string> TokenFromAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        var match = TokenRegex.Match(html);
        Assert.True(match.Success, "Antiforgery token not found in response HTML.");
        return match.Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> PostFormAsync(this HttpClient client, string path, string tokenPage, Dictionary<string, string> fields)
    {
        var page = await client.GetAsync(tokenPage);
        fields["__RequestVerificationToken"] = await TokenFromAsync(page);
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    public static async Task<HttpClient> LoginCmsAsync(this WebApplicationFactory<Program> factory, string email, string password = "Demo@12345")
    {
        var client = factory.NewClient();
        var response = await client.PostFormAsync("/cms/dang-nhap", "/cms/dang-nhap",
            new Dictionary<string, string> { ["Email"] = email, ["Password"] = password });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    public static bool SetsAuthCookie(HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var cookies)
           && cookies.Any(c => c.StartsWith(".AspNetCore.Identity.Application=", StringComparison.OrdinalIgnoreCase)
                               && !c.StartsWith(".AspNetCore.Identity.Application=;", StringComparison.OrdinalIgnoreCase));
}
