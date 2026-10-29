using VNews.Application.Common;

namespace VNews.Tests.Unit;

public class PagedResultTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(25, 10, 3)]
    public void TotalPages_ComputesCeilingOfCountOverPageSize(int totalCount, int pageSize, int expectedPages)
    {
        var result = new PagedResult<string>
        {
            Items = Array.Empty<string>(),
            Page = 1,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        Assert.Equal(expectedPages, result.TotalPages);
    }

    [Fact]
    public void TotalPages_WithZeroPageSize_DoesNotThrow()
    {
        var result = new PagedResult<string>
        {
            Items = Array.Empty<string>(),
            Page = 1,
            PageSize = 0,
            TotalCount = 5
        };

        Assert.Equal(0, result.TotalPages);
    }
}
