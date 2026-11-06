using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseProviderSearchTests
{
    [Fact]
    public async Task SuggestAsync_OneProviderFails_KeepsOtherCandidatesAndReportsFailure()
    {
        var row = new MuseRecommendation("Film", 2020, "note", "Films", ["https://example.com"], 1);
        var candidate = new MuseProviderCandidate(1, "Film", "2020-01-01", "", "", "TVMaze", "Show");
        Task<IReadOnlyList<MuseProviderCandidate>> Failed(string title, CancellationToken token) => throw new InvalidOperationException();
        Task<IReadOnlyList<MuseProviderCandidate>> Success(string title, CancellationToken token) => Task.FromResult<IReadOnlyList<MuseProviderCandidate>>([candidate]);

        var results = await new MuseSuggestionPlanner().SuggestAsync([row]
            , (title, token) => new MuseProviderSearch().SearchAsync(title, Failed, Success, token));

        var result = Assert.Single(results);
        Assert.Equal(candidate, Assert.Single(result.Candidates));
        Assert.Contains("TMDB", result.Error);
    }

    [Fact]
    public async Task SearchAsync_RetryAfterPartialFailure_ReusesSuccessfulProvider()
    {
        var service = new MuseProviderSearch();
        var movieCalls = 0;
        var showCalls = 0;
        Task<IReadOnlyList<MuseProviderCandidate>> Movies(string title, CancellationToken token)
        {
            movieCalls++;
            return Task.FromResult<IReadOnlyList<MuseProviderCandidate>>([]);
        }
        Task<IReadOnlyList<MuseProviderCandidate>> Shows(string title, CancellationToken token)
        {
            showCalls++;
            if (showCalls == 1) throw new InvalidOperationException();
            return Task.FromResult<IReadOnlyList<MuseProviderCandidate>>([]);
        }

        await Assert.ThrowsAsync<PartialProviderSearchException>(() => service.SearchAsync("Film", Movies, Shows, CancellationToken.None));
        await service.SearchAsync("Film", Movies, Shows, CancellationToken.None);

        Assert.Equal(1, movieCalls);
        Assert.Equal(2, showCalls);
    }
}
