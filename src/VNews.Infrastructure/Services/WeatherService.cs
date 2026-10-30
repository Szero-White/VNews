using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;

namespace VNews.Infrastructure.Services;

/// <summary>
/// Dữ liệu thời tiết thật lấy từ Open-Meteo (miễn phí, không cần API key) cho một địa phương cố định (Hà Nội).
/// Không có dữ liệu giả: khi gọi API lỗi, trả về null để giao diện ẩn khu vực thời tiết thay vì hiển thị số liệu bịa.
/// </summary>
public class WeatherService : IWeatherService
{
    private const string CacheKey = "weather:hanoi";
    private const double Latitude = 21.0285;
    private const double Longitude = 105.8542;

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(HttpClient httpClient, IMemoryCache cache, ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<WeatherSnapshotDto?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out WeatherSnapshotDto? cached))
        {
            return cached;
        }

        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&current=temperature_2m&timezone=Asia%2FBangkok";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var temperature = doc.RootElement.GetProperty("current").GetProperty("temperature_2m").GetDouble();

            var snapshot = new WeatherSnapshotDto("Hà Nội", temperature, "Open-Meteo");
            _cache.Set(CacheKey, snapshot, TimeSpan.FromHours(2));
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không lấy được dữ liệu thời tiết từ Open-Meteo.");
            return null;
        }
    }
}
