using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        var aggregatedResult = new AggregatedResult<MovieSearchResponse>
                               {
                                   Data = new MovieSearchResponse
                                          {
                                              Results = new List<MovieSearchResult>()
                                          }
                               };

        if (string.IsNullOrWhiteSpace(query))
        {
            return aggregatedResult;
        }

        // Run queries concurrently across all active providers
        var searchTasks = _providers.Select(async provider =>
        {
            try
            {
                var result = await provider.SearchMoviesAsync(query);
                return (ProviderName: provider.GetType().Name, Result: result);
            }
            catch (Exception ex)
            {
                var errorResult = new AggregatedResult<MovieSearchResponse?>();
                errorResult.Diagnostics[provider.GetType().Name] = $"Exception: {ex.Message}";
                return (ProviderName: provider.GetType().Name, Result: errorResult);
            }
        }).ToList();

        var completedResults = await Task.WhenAll(searchTasks);

        var allResults = new List<(string ProviderName, MovieSearchResult Item)>();

        foreach (var completed in completedResults)
        {
            // Merge diagnostics
            if (completed.Result?.Diagnostics != null)
            {
                foreach (var diag in completed.Result.Diagnostics)
                {
                    aggregatedResult.Diagnostics[diag.Key] = diag.Value;
                }
            }

            if (completed.Result?.Data?.Results != null)
            {
                foreach (var item in completed.Result.Data.Results)
                {
                    if (item != null)
                    {
                        allResults.Add((completed.ProviderName, item));
                    }
                }
            }
        }

        // Group by normalized title to deduplicate
        var groupedResults = allResults.GroupBy(result => NormalizeTitle(result.Item.Title))
                                       .Where(group => !string.IsNullOrEmpty(group.Key));

        var mergedResults = new List<MovieSearchResult>();

        foreach (var group in groupedResults)
        {
            // Prefer TMDB first, then JustWatch, then Utelly for the primary representative
            var primaryRepresentative = group.OrderBy(result => result.ProviderName == "TmdbService" ? 0 
                                                              : result.ProviderName == "JustWatchService" ? 1 
                                                              : 2)
                                             .Select(result => result.Item)
                                             .FirstOrDefault();

            if (primaryRepresentative != null)
            {
                // Merge streaming providers from all items in this group
                var allProvidersForGroup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var result in group)
                {
                    if (result.Item.StreamingProviders != null)
                    {
                        foreach (var providerName in result.Item.StreamingProviders)
                        {
                            if (!string.IsNullOrWhiteSpace(providerName))
                            {
                                allProvidersForGroup.Add(providerName);
                            }
                        }
                    }
                }

                primaryRepresentative.StreamingProviders = allProvidersForGroup.ToList();
                mergedResults.Add(primaryRepresentative);
            }
        }

        aggregatedResult.Data.Results = mergedResults;
        return aggregatedResult;
    }

    private string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var normalized = title.ToLowerInvariant();
        var chars = normalized.Where(c => char.IsLetterOrDigit(c)).ToArray();
        return new string(chars);
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
                       Data        = searchResponse.Data?.Results?.FirstOrDefault()
                     , Diagnostics = searchResponse.Diagnostics
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
