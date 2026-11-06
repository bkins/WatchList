using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseSuggestionPlannerTests
{
    [Fact]
    public async Task SuggestAsync_RepeatedTitle_SearchesOnceAndRanksEachYearWithoutApproving()
    {
        var first = new MuseRecommendation("Film", 2001, "note", "Films", ["https://example.com/one"], 1);
        var second = new MuseRecommendation("Film", 2020, "note", "Films", ["https://example.com/two"], 2);
        var calls = 0;
        Task<IReadOnlyList<MuseProviderCandidate>> Search(string title, CancellationToken cancellationToken)
        {
            calls++;
            IReadOnlyList<MuseProviderCandidate> results =
            [
                new(2, "Film", "2020-01-01", "", "", "TMDB", "Movie")
              , new(1, "Film", "2001-01-01", "", "", "TMDB", "Movie")
            ];
            return Task.FromResult(results);
        }

        var results = await new MuseSuggestionPlanner().SuggestAsync([first, second], Search);

        Assert.Equal(1, calls);
        Assert.Equal(1, results[0].Candidates[0].Id);
        Assert.Equal(2, results[1].Candidates[0].Id);
        Assert.Equal(2, results[0].Candidates.Count);
    }

    [Fact]
    public async Task SuggestAsync_SearchFailure_ReportsRowError()
    {
        var row = new MuseRecommendation("Film", null, "note", "Films", ["https://example.com"], 1);

        var result = await new MuseSuggestionPlanner().SuggestAsync([row], (title, token) => throw new InvalidOperationException());

        Assert.NotNull(Assert.Single(result).Error);
        Assert.Empty(result[0].Candidates);
    }

    [Fact]
    public async Task SuggestAsync_OneSearchFinishes_ReportsBeforeOtherSearchCompletes()
    {
        var first = new MuseRecommendation("First", null, "note", "Films", ["https://example.com/one"], 1);
        var second = new MuseRecommendation("Second", null, "note", "Films", ["https://example.com/two"], 2);
        var delayed = new TaskCompletionSource<IReadOnlyList<MuseProviderCandidate>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reported = new TaskCompletionSource<MuseSuggestionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<IReadOnlyList<MuseProviderCandidate>> Search(string title, CancellationToken token)
        {
            return title == "First" ? Task.FromResult<IReadOnlyList<MuseProviderCandidate>>([]) : delayed.Task;
        }

        var all = new MuseSuggestionPlanner().SuggestAsync([first, second], Search, onCompleted: result => reported.TrySetResult(result));
        var firstResult = await reported.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("First", firstResult.Recommendation.Title);
        Assert.False(all.IsCompleted);
        delayed.SetResult([]);
        Assert.Equal(2, (await all).Count);
    }
}
