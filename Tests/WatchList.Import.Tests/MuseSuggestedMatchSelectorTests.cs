using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseSuggestedMatchSelectorTests
{
    [Fact]
    public void FindExistingIdentity_EqualNumericIdFromDifferentProvider_DoesNotLink()
    {
        var candidate = new MuseProviderCandidate(158, "Fringe", "2008", "", "", "TVMaze", "Show");
        var saved = new MuseExistingItem(Guid.NewGuid(), "Other film", false, 158, "TMDB");

        var existing = new MuseSuggestedMatchSelector().FindExistingIdentity(candidate, [saved]);

        Assert.Null(existing);
    }

    [Fact]
    public void FindExistingIdentity_SameTitleDifferentRelease_DoesNotSelectSavedIdentity()
    {
        var row = new MuseRecommendation("Dune", 2021, "", "Films", [], 1);
        var older = new MuseProviderCandidate(841, "Dune", "1984", "", "", "TMDB", "Movie");
        var newer = new MuseProviderCandidate(438631, "Dune", "2021", "", "", "TMDB", "Movie");
        var saved = new MuseExistingItem(Guid.NewGuid(), "Dune", false, 841, "TMDB");
        var selector = new MuseSuggestedMatchSelector();

        var suggestion = selector.SelectUnambiguous(row, [older, newer]);
        var existing = selector.FindExistingIdentity(suggestion!, [saved]);

        Assert.Same(newer, suggestion);
        Assert.Null(existing);
    }

    [Fact]
    public void FindExistingIdentity_ExactProviderIdentity_SelectsSavedItem()
    {
        var candidate = new MuseProviderCandidate(438631, "Dune", "2021", "", "", "TMDB", "Movie");
        var saved = new MuseExistingItem(Guid.NewGuid(), "Dune", false, 438631, "tmdb");

        var existing = new MuseSuggestedMatchSelector().FindExistingIdentity(candidate, [saved]);

        Assert.Same(saved, existing);
    }

    [Fact]
    public void FindExistingIdentity_DuplicateSavedIdentities_RequiresExplicitChoice()
    {
        var candidate = new MuseProviderCandidate(841, "Dune", "1984", "", "", "TMDB", "Movie");
        var first = new MuseExistingItem(Guid.NewGuid(), "Dune", false, 841, "TMDB");
        var second = new MuseExistingItem(Guid.NewGuid(), "Dune", true, 841, "TMDB");

        var existing = new MuseSuggestedMatchSelector().FindExistingIdentity(candidate, [first, second]);

        Assert.Null(existing);
    }

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
