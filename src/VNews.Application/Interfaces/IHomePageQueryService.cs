using VNews.Application.Common;

namespace VNews.Application.Interfaces;

/// <summary>Everything the home page shows, read from the database in one go.</summary>
public interface IHomePageQueryService
{
    Task<HomePageData> GetHomePageDataAsync(CancellationToken cancellationToken = default);
}
