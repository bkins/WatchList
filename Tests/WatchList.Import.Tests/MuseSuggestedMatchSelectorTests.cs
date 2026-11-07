using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseSuggestedMatchSelectorTests
{
    [Fact]
    public void SelectUnambiguous_ExactTitleAndYear_SelectsOnlyMatchingIdentity()
    {
        var row = new MuseRecommendation("Dune", 2021, "", "Films", [], 1);
        var older = new MuseProviderCandidate(1, "Dune", "1984-01-01", "", "", "TMDB", "Movie");
        var newer = new MuseProviderCandidate(2, "Dune", "2021-01-01", "", "", "TMDB", "Movie");

        var selected = new MuseSuggestedMatchSelector().SelectUnambiguous(row, [older, newer]);

        Assert.Same(newer, selected);
    }

    [Fact]
    public void SelectUnambiguous_MovieAndShowWithSameTitle_RequiresChoice()
    {
        var row = new MuseRecommendation("Film", null, "", "Films", [], 1);
        var movie = new MuseProviderCandidate(1, "Film", "2021", "", "", "TMDB", "Movie");
        var show = new MuseProviderCandidate(2, "Film", "2021", "", "", "TVMaze", "Show");

        var selected = new MuseSuggestedMatchSelector().SelectUnambiguous(row, [movie, show]);

        Assert.Null(selected);
    }

    [Fact]
    public void SelectUnambiguous_NoExactIdentity_DoesNotGuess()
    {
        var row = new MuseRecommendation("Film", 2021, "", "Films", [], 1);
        var candidate = new MuseProviderCandidate(1, "Different film", "2021", "", "", "TMDB", "Movie");

        var selected = new MuseSuggestedMatchSelector().SelectUnambiguous(row, [candidate]);

        Assert.Null(selected);
    }
}
