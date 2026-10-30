using VNews.Application.DTOs;

namespace VNews.Application.Interfaces;

public interface IWeatherService
{
    Task<WeatherSnapshotDto?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
