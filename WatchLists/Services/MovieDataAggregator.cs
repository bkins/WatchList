using WatchLists.DataAccess.Interfaces;
using WatchLists.Services.Interfaces;
using WatchLists.Services.Models;

namespace WatchLists.Services;

public class MovieDataAggregator : IMovieDataAggregator
{
    private readonly List<IMovieDataProvider> _providers;

    public MovieDataAggregator(IEnumerable<IMovieDataProvider> providers)
    {
        _providers = providers.Where(p => p.IsEnabled).ToList();
    }

    /// <summary>
    /// Executes a given function across all providers and aggregates the results with diagnostics.
    /// </summary>
    public async Task<AggregatedResult<T>> ExecuteWithDiagnosticsAsync<T> (Func<IMovieDataProvider
                                                                                 , Task<AggregatedResult<T>>> operation)
    {
        var aggregatedResult   = new AggregatedResult<T>();
        T?  lastSuccessfulData = default;

        foreach (var provider in _providers)
        {
            var result = await operation(provider); // This already handles exceptions internally

            // Merge diagnostics from the provider's response
            foreach (var diagnostic in result.Diagnostics)
            {
                aggregatedResult.Diagnostics[diagnostic.Key] = diagnostic.Value;
            }

            // Keep the last successful data instead of returning early
            if (result.Data != null)
            {
                lastSuccessfulData = result.Data;
            }
        }

        // Assign the last successful data if available
        aggregatedResult.Data = lastSuccessfulData;

        return aggregatedResult;
    }

    public async Task<AggregatedResult<MovieSearchResponse>> SearchMoviesAsync(string query)
    {
        return await ExecuteWithDiagnosticsAsync(provider => provider.SearchMoviesAsync(query));
    }

    public async Task<AggregatedResult<WatchProvidersResponse>> GetWatchProvidersAsync(int movieId)
    {
        return await ExecuteWithDiagnosticsAsync(provider => provider.GetWatchProvidersAsync(movieId));
    }

    public async Task<AggregatedResult<MovieDetail>> GetMovieDetailsAsync(int movieId)
    {
        return await ExecuteWithDiagnosticsAsync(provider => provider.GetMovieDetailsAsync(movieId));
    }

    public async Task<AggregatedResult<MovieSearchResult>> SearchMovieAsync (string searchQuery)
    {
        return await ExecuteWithDiagnosticsAsync(async provider =>
        {
            var searchResponse = await provider.SearchMoviesAsync(searchQuery);

            return new AggregatedResult<MovieSearchResult>
                   {
                           Data        = searchResponse.Data?.Results.Values.FirstOrDefault(), // Extract only the MovieSearchResult
                           Diagnostics = searchResponse.Diagnostics
                   };
        });
    }

    public async Task<object?> SearchTVShowsAsync (string queryParameter)
    {
        throw new NotImplementedException();
    }

    public Task<AggregatedResult<TvDetail>> GetTVShowDetailsAsync (int arg)
    {
        throw new NotImplementedException();
    }

    public async Task<object?> SearchPeopleAsync (string queryParameter)
    {
        throw new NotImplementedException();
    }

    public Task<AggregatedResult<Person>> GetPersonDetailsAsync (int arg)
    {
        throw new NotImplementedException();
    }

    public async Task<object?> GetTrendingMoviesAsync()
    {
        throw new NotImplementedException();
    }

    public async Task<object?> GetTrendingTVShowsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<AggregatedResult<Video>> GetMovieVideosAsync (int arg)
    {
        throw new NotImplementedException();
    }

    public Task<AggregatedResult<Video>> GetTVVideosAsync (int arg)
    {
        throw new NotImplementedException();
    }
}
