using System.Collections.Concurrent;

namespace WatchLists.Services.MuseImport;

public sealed class MuseSuggestionPlanner
{
    public async Task<IReadOnlyList<MuseSuggestionResult>> SuggestAsync(IReadOnlyList<MuseRecommendation> recommendations
                                                                      , Func<string, CancellationToken, Task<IReadOnlyList<MuseProviderCandidate>>> search
                                                                      , CancellationToken cancellationToken = default
                                                                      , Action<MuseSuggestionResult>? onCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(recommendations);
        ArgumentNullException.ThrowIfNull(search);

        using var limiter = new SemaphoreSlim(4, 4);
        var cache = new ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<MuseProviderCandidate>>>>(StringComparer.OrdinalIgnoreCase);
        async Task<IReadOnlyList<MuseProviderCandidate>> SearchOnce(string title)
        {
            await limiter.WaitAsync(cancellationToken);
            try
            {
                return await search(title, cancellationToken);
            }
            finally
            {
                limiter.Release();
            }
        }

        async Task<MuseSuggestionResult> SuggestRow(MuseRecommendation row)
        {
            MuseSuggestionResult result;
            try
            {
                var pending = cache.GetOrAdd(row.Title.Trim(), title => new Lazy<Task<IReadOnlyList<MuseProviderCandidate>>>(() => SearchOnce(title)));
                var candidates = await pending.Value;
                var ranked = candidates.OrderBy(candidate => Rank(row, candidate)).ToList();
                result = new MuseSuggestionResult(row, ranked, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                result = new MuseSuggestionResult(row, [], "Search failed; retry or review manually.");
            }
            onCompleted?.Invoke(result);
            return result;
        }

        return await Task.WhenAll(recommendations.Select(SuggestRow));
    }

    private static int Rank(MuseRecommendation row, MuseProviderCandidate candidate)
    {
        var exactTitle = string.Equals(row.Title.Trim(), candidate.Title.Trim(), StringComparison.OrdinalIgnoreCase);
        var yearMatches = row.ReleaseYear.HasValue && candidate.Date.Length >= 4 &&
                          int.TryParse(candidate.Date.AsSpan(0, 4), out var year) && year == row.ReleaseYear;
        return exactTitle ? yearMatches ? 0 : 1 : 2;
    }
}
