using System.Net;
using System.Net.Sockets;
using VNews.Application.Interfaces;

namespace VNews.Web.Services;

public interface IRemoteImageFetcher
{
    /// <summary>Downloads the picture at a public web address and stores it like an upload (same checks, same folder), so the article no longer depends on the other site.</summary>
    Task<ImageUploadResult> ImportAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>
/// Saves a picture that was pasted into an article together with its text. The address comes from the browser, so it is untrusted:
/// only http(s) addresses without credentials are fetched, every connection is checked against <see cref="RemoteAddressPolicy"/> (so the
/// server cannot be pointed at its own machine or network, also through redirects), the wait and the size are capped, and the bytes then go
/// through the same <see cref="IImageUploadService"/> checks as a file chosen from disk.
/// </summary>
public sealed class RemoteImageFetcher : IRemoteImageFetcher
{
    public const string ClientName = "remote-images";
    private const int MaxUrlLength = 2000;

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg", ["image/jpg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp",
    };

    private readonly HttpClient _http;
    private readonly IImageUploadService _uploads;
    private readonly ILogger<RemoteImageFetcher> _logger;

    public RemoteImageFetcher(HttpClient http, IImageUploadService uploads, ILogger<RemoteImageFetcher> logger)
    {
        _http = http;
        _uploads = uploads;
        _logger = logger;
    }

    public static bool IsAcceptableUrl(string? url, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(url) || url.Length > MaxUrlLength) return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) return false;
        if (!string.IsNullOrEmpty(parsed.UserInfo)) return false;
        uri = parsed;
        return true;
    }

    public async Task<ImageUploadResult> ImportAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!IsAcceptableUrl(url, out var uri)) return Fail("Địa chỉ ảnh không hợp lệ.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.ParseAdd("image/jpeg,image/png,image/webp");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return Fail("Không tải được ảnh (máy chủ nguồn trả lỗi).");

            var mime = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!Extensions.TryGetValue(mime, out var extension)) return Fail("Chỉ lưu được ảnh JPG, PNG hoặc WebP.");

            var declared = response.Content.Headers.ContentLength;
            if (declared > ImageUploadLimits.MaxBytes) return Fail("Ảnh lớn hơn " + ImageUploadLimits.Label + ".");

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await body.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > ImageUploadLimits.MaxBytes) return Fail("Ảnh lớn hơn " + ImageUploadLimits.Label + ".");
                buffer.Write(chunk, 0, read);
            }

            buffer.Position = 0;
            var contentType = mime.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : mime;
            return await _uploads.SaveAsync(buffer, "anh-nhap" + extension, contentType, buffer.Length, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            // Includes the policy refusing a private address: the reader sees the same short message either way.
            _logger.LogWarning("Remote image import failed for host {Host}: {Reason}", uri.Host, ex.GetBaseException().Message);
            return Fail("Không tải được ảnh từ địa chỉ này.");
        }
    }

    private static ImageUploadResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

/// <summary>Which addresses the server may connect to when it fetches something for a user: public internet addresses only.</summary>
public static class RemoteAddressPolicy
{
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return false;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.IsIPv6UniqueLocal);
        }

        var b = address.GetAddressBytes();
        return !(b[0] == 0                                         // "this" network
            || b[0] == 10                                          // private
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)          // carrier-grade NAT
            || b[0] == 127                                         // loopback
            || (b[0] == 169 && b[1] == 254)                        // link-local (cloud metadata lives here)
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)           // private
            || (b[0] == 192 && b[1] == 0 && b[2] == 0)             // protocol assignments
            || (b[0] == 192 && b[1] == 168)                        // private
            || (b[0] == 198 && (b[1] == 18 || b[1] == 19))         // benchmarking
            || b[0] >= 224);                                       // multicast and reserved
    }

    /// <summary>A handler that resolves the name itself and connects only to a public address, on every request and redirect.</summary>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        UseProxy = false,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 3,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        ConnectCallback = async (context, cancellationToken) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
            var target = addresses.FirstOrDefault(IsPublic)
                ?? throw new InvalidOperationException("The address is not a public internet address.");

            var socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(target, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };
}
