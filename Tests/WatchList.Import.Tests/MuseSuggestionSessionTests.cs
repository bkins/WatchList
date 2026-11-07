using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseSuggestionSessionTests
{
    [Fact]
    public async Task SearchPendingAsync_CancelThenResume_PreservesResultsAndApprovals()
    {
        var first = new MuseRecommendation("First", null, "note", "Films", ["https://example.com/one"], 1);
        var second = new MuseRecommendation("Second", null, "note", "Films", ["https://example.com/two"], 2);
        var session = new MuseSuggestionSession([first, second]);
        session.Review.ApproveExisting(0, new WatchItem { Title = "First" });
        using var cancellation = new CancellationTokenSource();
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<IReadOnlyList<MuseProviderCandidate>> InitialSearch(string title, CancellationToken token)
        {
            if (title == "Second") await Task.Delay(Timeout.Infinite, token);
            return [];
        }

        var running = session.SearchPendingAsync(InitialSearch, result => reported.TrySetResult(), cancellation.Token);
        await reported.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        var searched = new List<string>();
        await session.SearchPendingAsync((title, token) =>
        {
            searched.Add(title);
            return Task.FromResult<IReadOnlyList<MuseProviderCandidate>>([]);
        }, result => { });

        Assert.Equal(new[] { "Second" }, searched);
        Assert.Equal(2, session.Completed.Count);
        Assert.Equal(1, session.Review.ApprovedCount);
    }

    [Fact]
    public async Task ResetApprovals_AfterSearch_PreservesCompletedSuggestions()
    {
        var row = new MuseRecommendation("First", null, "note", "Films", ["https://example.com/one"], 1);
        var session = new MuseSuggestionSession([row]);
        session.Review.ApproveExisting(0, new WatchItem { Title = "First" });
        await session.SearchPendingAsync((title, token) => Task.FromResult<IReadOnlyList<MuseProviderCandidate>>([]), result => { });

        session.ResetApprovals();

        Assert.Equal(0, session.Review.ApprovedCount);
        Assert.Single(session.Completed);
    }

    [Fact]
    public async Task SearchPendingAsync_PartialRetryRetainsEarlierProviderCandidates()
    {
        var row = new MuseRecommendation("Film", null, "note", "Films", ["https://example.com/one"], 1);
        var session = new MuseSuggestionSession([row]);
        var movie = new MuseProviderCandidate(1, "Film", "2020", "", "", "TMDB", "Movie");
        var show = new MuseProviderCandidate(2, "Film", "2021", "", "", "TVMaze", "Show");
        Task<IReadOnlyList<MuseProviderCandidate>> First(string title, CancellationToken token) => throw new PartialProviderSearchException("TVMaze failed", [movie]);
        Task<IReadOnlyList<MuseProviderCandidate>> Second(string title, CancellationToken token) => throw new PartialProviderSearchException("TMDB failed", [show]);

        await session.SearchPendingAsync(First, result => { });
        await session.SearchPendingAsync(Second, result => { });

        Assert.Equal(2, session.Completed[row].Candidates.Count);
        Assert.NotNull(session.Completed[row].Error);
    }
    [Fact]
    public async Task SearchPendingAsync_LargeReviewStoppedAndResumed_PreservesProgressAndReusesSearches()
    {
        var rows = Enumerable.Range(0, 201)
                             .Select(index => new MuseRecommendation(index < 2 ? "Completed title" : index < 4 ? "Shared pending title" : $"Title {index}"
                                                                    , null
                                                                    , "Synthetic recommendation"
                                                                    , "Films"
                                                                    , [$"https://example.com/source/{index}"]
                                                                    , index + 1))
                             .ToList();
        var session = new MuseSuggestionSession(rows);
        session.Review.ApproveExisting(0, new WatchItem { Title = "Completed title" });
        using var cancellation = new CancellationTokenSource();
        var completedRows = 0;
        var firstResultsReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<IReadOnlyList<MuseProviderCandidate>> InitialSearch(string title, CancellationToken token)
        {
            if (title != "Completed title") await Task.Delay(Timeout.Infinite, token);
            return [];
        }

        var running = session.SearchPendingAsync(InitialSearch, result =>
        {
            if (Interlocked.Increment(ref completedRows) == 2) firstResultsReady.TrySetResult();
        }, cancellation.Token);
        await firstResultsReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        var searches = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
        await session.SearchPendingAsync((title, token) =>
        {
            searches.AddOrUpdate(title, 1, (key, count) => count + 1);
            return Task.FromResult<IReadOnlyList<MuseProviderCandidate>>([]);
        }, result => { });

        Assert.Equal(201, session.Completed.Count);
        Assert.Equal(1, session.Review.ApprovedCount);
        Assert.False(searches.ContainsKey("Completed title"));
        Assert.Equal(1, searches["Shared pending title"]);
        Assert.Equal(198, searches.Count);
    }
}
