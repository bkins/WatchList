using System.Collections.Concurrent;

namespace WatchLists.Services.MuseImport;

public sealed class MuseProviderSearch
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<MuseProviderCandidate>> _movieCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IReadOnlyList<MuseProviderCandidate>> _showCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<MuseProviderCandidate>> SearchAsync(string title
                                                                       , Func<string, CancellationToken, Task<IReadOnlyList<MuseProviderCandidate>>> movieSearch
                                                                       , Func<string, CancellationToken, Task<IReadOnlyList<MuseProviderCandidate>>> showSearch
                                                                       , CancellationToken cancellationToken)
    {
        async Task<(IReadOnlyList<MuseProviderCandidate> Candidates, bool Failed)> SearchProvider(Func<string, CancellationToken, Task<IReadOnlyList<MuseProviderCandidate>>> search
                                                                                                 , ConcurrentDictionary<string, IReadOnlyList<MuseProviderCandidate>> cache)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (cache.TryGetValue(title, out var cached)) return (cached, false);
                var found = await search(title, cancellationToken);
                cache[title] = found;
                return (found, false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return ([], true);
            }
        }

        var movies = SearchProvider(movieSearch, _movieCache);
        var shows  = SearchProvider(showSearch, _showCache);
        try
        {
            await Task.WhenAll(movies, shows);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var completedCandidates = new List<MuseProviderCandidate>();
            var completedProvider   = false;
            if (movies.IsCompletedSuccessfully)
            {
                var completedMovies = await movies;
                if (!completedMovies.Failed)
                {
                    completedProvider = true;
                    completedCandidates.AddRange(completedMovies.Candidates);
                }
            }
            if (shows.IsCompletedSuccessfully)
            {
                var completedShows = await shows;
                if (!completedShows.Failed)
                {
                    completedProvider = true;
                    completedCandidates.AddRange(completedShows.Candidates);
                }
            }
            if (!completedProvider) throw;
            throw new PartialProviderSearchException("Searches stopped; completed suggestions are shown. Resume to retry pending providers."
                                                   , completedCandidates);
        }
        var movieResult = await movies;
        var showResult  = await shows;
        var candidates = movieResult.Candidates.Concat(showResult.Candidates).ToList();
        if (movieResult.Failed || showResult.Failed)
        {
            var failed = movieResult.Failed && showResult.Failed ? "TMDB and TVMaze" : movieResult.Failed ? "TMDB" : "TVMaze";
            throw new PartialProviderSearchException($"{failed} search failed; available results are shown. Resume to retry.", candidates);
        }
        return candidates;
    }
}
