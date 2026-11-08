using System.Text.Json;
using WatchLists.Services.Models;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MovieSearchResultTests
{
    [Fact]
    public void Deserialize_OmittedPresentationFields_RetainsIdentityAndHasSafeEmptyPoster()
    {
        var result = JsonSerializer.Deserialize<MovieSearchResult>("{\"id\":123,\"title\":\"Synthetic\"}");

        Assert.NotNull(result);
        Assert.Equal(123, result.Id);
        Assert.Equal("Synthetic", result.Title);
        Assert.Equal(string.Empty, result.Overview);
        Assert.Equal(string.Empty, result.PosterUrl);
    }
}
