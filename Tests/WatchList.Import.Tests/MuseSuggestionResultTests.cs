using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseSuggestionResultTests
{
    [Fact]
    public void NeedsAttention_OneExactMatchAmongAlternatives_ReturnsFalse()
    {
        var row = new MuseRecommendation("Dune", 2021, "", "Films", [], 1);
        var original = new MuseProviderCandidate(1, "Dune", "1984", "", "", "TMDB", "Movie");
        var intended = new MuseProviderCandidate(2, "Dune", "2021", "", "", "TMDB", "Movie");

        var result = new MuseSuggestionResult(row, [original, intended], null);

        Assert.False(result.NeedsAttention);
    }

    [Fact]
    public void NeedsAttention_AmbiguousExactIdentities_ReturnsTrue()
    {
        var row = new MuseRecommendation("Film", null, "", "Films", [], 1);
        var movie = new MuseProviderCandidate(1, "Film", "2021", "", "", "TMDB", "Movie");
        var show = new MuseProviderCandidate(2, "Film", "2021", "", "", "TVMaze", "Show");

        var result = new MuseSuggestionResult(row, [movie, show], null);

        Assert.True(result.NeedsAttention);
    }

    [Fact]
    public void NeedsAttention_ProviderFailureWithUsableMatch_RemainsVisible()
    {
        var row = new MuseRecommendation("Film", null, "", "Films", [], 1);
        var match = new MuseProviderCandidate(1, "Film", "2021", "", "", "TMDB", "Movie");

        var result = new MuseSuggestionResult(row, [match], "TV provider failed");

        Assert.True(result.NeedsAttention);
    }

    [Fact]
    public void NeedsAttention_OnlyWrongIdentity_ReturnsTrue()
    {
        var row = new MuseRecommendation("Film", null, "", "Films", [], 1);
        var match = new MuseProviderCandidate(1, "Different film", "2021", "", "", "TMDB", "Movie");

        var result = new MuseSuggestionResult(row, [match], null);

        Assert.True(result.NeedsAttention);
    }
}
