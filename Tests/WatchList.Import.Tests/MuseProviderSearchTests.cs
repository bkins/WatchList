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
    [Fact]
    public async Task SearchAsync_CompletedShowAndCancelledMovie_ReturnsPartialShow()
    {
        var service = new MuseProviderSearch();
        var show = new MuseProviderCandidate(15, "Synthetic show", "2020", "", "", "TVMaze", "Show");
        var pendingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        async Task<IReadOnlyList<MuseProviderCandidate>> PendingMovie(string title, CancellationToken token)
        {
            pendingStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return [];
        }
        Task<IReadOnlyList<MuseProviderCandidate>> FinishedShow(string title, CancellationToken token)
        {
            return Task.FromResult<IReadOnlyList<MuseProviderCandidate>>([show]);
        }

        var search = service.SearchAsync("Synthetic show", PendingMovie, FinishedShow, cancellation.Token);
        await pendingStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var result = await Assert.ThrowsAsync<PartialProviderSearchException>(() => search);

        Assert.Equal(show, Assert.Single(result.Candidates));
        Assert.Contains("stopped", result.Message);
    }

    [Fact]
    public async Task SearchAsync_BothProvidersCancelled_PropagatesCancellation()
    {
        var service = new MuseProviderSearch();
        var started = 0;
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        async Task<IReadOnlyList<MuseProviderCandidate>> Pending(string title, CancellationToken token)
        {
            if (Interlocked.Increment(ref started) == 2) bothStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return [];
        }

        var search = service.SearchAsync("Synthetic title", Pending, Pending, cancellation.Token);
        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => search);
    }
}
