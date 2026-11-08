using WatchLists.Services.Models;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class SearchResultIdentityTests
{
    [Theory]
    [InlineData("TmdbService", "Show", 1, "1982-01-01")]
    [InlineData("TmdbService", "Movie", 2, "1982-01-01")]
    [InlineData("TmdbService", "Movie", 1, "2011-01-01")]
    [InlineData("TvMazeService", "Movie", 1, "1982-01-01")]
    public void Create_DistinctIdentityComponents_DoNotMatchOtherRelease(string providerName
                                                                      , string mediaType
                                                                      , int    nativeId
                                                                      , string releaseDate)
    {
        var result = new MovieSearchResult
                     {
                         Id          = nativeId
                       , MediaType   = mediaType
                       , ReleaseDate = releaseDate
                     };
        var otherRelease = new MovieSearchResult
                           {
                               Id          = 1
                             , MediaType   = "Movie"
                             , ReleaseDate = "1982-01-01"
                           };

        var identity = SearchResultIdentity.Create(providerName, result);
        var otherIdentity = SearchResultIdentity.Create("TmdbService", otherRelease);

        Assert.NotNull(identity);
        Assert.NotEqual(otherIdentity, identity);
    }

    [Fact]
    public void Create_SameNativeIdentityWithDifferentTitleAndDay_Matches()
    {
        var first = new MovieSearchResult
                    {
                        Id          = 1
                      , Title       = "Original"
                      , ReleaseDate = "1982-01-01"
                    };
        var second = new MovieSearchResult
                     {
                         Id          = 1
                       , Title       = "Localized"
                       , MediaType   = "movie"
                       , ReleaseDate = "1982-12-01"
                     };

        var firstIdentity = SearchResultIdentity.Create("TmdbService", first);
        var secondIdentity = SearchResultIdentity.Create("TmdbService", second);

        Assert.Equal(firstIdentity, secondIdentity);
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(123, "")]
    [InlineData(123, "https://www.imdb.com/find/?q=Title")]
    [InlineData(123, "https://example.com/title/tt123/")]
    public void Create_HashedOrMissingIdentity_ReturnsNoMatch(int nativeId, string webUrl)
    {
        var result = new MovieSearchResult
                     {
                         Id     = nativeId
                       , Title  = "Same"
                       , WebUrl = webUrl
                     };

        var identity = SearchResultIdentity.Create("OmdbService", result);

        Assert.Null(identity);
    }

    [Fact]
    public void Create_ImdbUrl_UsesOriginalIdentityInsteadOfHash()
    {
        var first = new MovieSearchResult
                    {
                        Id     = 12
                      , WebUrl = "https://www.imdb.com/title/tt123/"
                    };
        var second = new MovieSearchResult
                     {
                         Id     = 99
                       , WebUrl = "https://imdb.com/title/tt123"
                     };

        var identity = SearchResultIdentity.Create("OmdbService", first);
        var sameIdentity = SearchResultIdentity.Create("OmdbService", second);
        var otherProvider = SearchResultIdentity.Create("FmDbService", second);

        Assert.NotNull(identity);
        Assert.Equal(identity, sameIdentity);
        Assert.NotEqual(identity, otherProvider);
    }
}
