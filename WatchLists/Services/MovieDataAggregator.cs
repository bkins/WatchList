using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WatchLists.DataAccess.Interfaces;
using WatchLists.ExtensionMethods;
using WatchLists.Services.Interfaces;
using WatchLists.Services.Models;
using WatchLists.MVVM.Models;

namespace WatchLists.Services;

public class MovieDataAggregator : IMovieDataAggregator
{
    private readonly List<IMovieDataProvider> _providers;

    public MovieDataAggregator(IEnumerable<IMovieDataProvider> providers)
    {
        _providers = providers.Where(provider => provider.IsEnabled).ToList();
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

            // Keep the first successful data (preferring primary providers like TMDB)
            if (lastSuccessfulData == null && result.Data != null)
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

            if (completed.Result?.Data?.Results == null) continue;

            allResults.AddRange(completed.Result
                                         .Data
                                         .Results
                                         .OfType<MovieSearchResult>()
                                         .Select(item => (completed.ProviderName, item)));
        }

        // Group by normalized title to deduplicate
        var groupedResults = allResults.GroupBy(result => NormalizeTitle(result.Item.Title))
                                       .Where(group => !string.IsNullOrEmpty(group.Key));

        var mergedResults = new List<MovieSearchResult>();

        foreach (var group in groupedResults)
        {
            // Prefer TMDB first, then OMDB, then FM-DB for the primary representative
            var primaryRepresentative = group.OrderBy(result => result.ProviderName == "TmdbService" ? 0
                                                              : result.ProviderName == "OmdbService" ? 1
                                                              : result.ProviderName == "FmDbService" ? 2
                                                              : 3)
                                             .Select(result => result.Item)
                                             .FirstOrDefault();

            if (primaryRepresentative == null) continue;

            var aggregatedData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Merge streaming providers, source APIs, and aggregate all fields from all items in this group
            var allProvidersForGroup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceApisForGroup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var result in group)
            {
                var apiName = result.ProviderName.Replace("Service", "");
                AggregateProperties(apiName, result.Item, aggregatedData);

                if (result.Item.AggregatedData != null)
                {
                    foreach (var kvp in result.Item.AggregatedData)
                    {
                        aggregatedData[kvp.Key] = kvp.Value;
                    }
                }

                if (result.Item.SourceApis != null && result.Item.SourceApis.Count > 0)
                {
                    foreach (var src in result.Item.SourceApis)
                    {
                        if (src.HasValue()) sourceApisForGroup.Add(src);
                    }
                }
                else
                {
                    sourceApisForGroup.Add(apiName);
                }

                if (result.Item.StreamingProviders == null) continue;

                foreach (var providerName in result.Item
                                                   .StreamingProviders
                                                   .Where(providerName => !string.IsNullOrWhiteSpace(providerName)))
                {
                    allProvidersForGroup.Add(providerName);
                }
            }

            primaryRepresentative.StreamingProviders = allProvidersForGroup.ToList();
            primaryRepresentative.SourceApis = sourceApisForGroup.ToList();
            primaryRepresentative.AggregatedData = aggregatedData;
            if (primaryRepresentative.PrimarySourceApi.HasNoValue())
            {
                primaryRepresentative.PrimarySourceApi = sourceApisForGroup.FirstOrDefault() ?? "Unknown";
            }
            mergedResults.Add(primaryRepresentative);
        }

        var sortedMergedResults = mergedResults.OrderBy(item => item.PrimarySourceApi == "Google" ? 1 : 0)
                                               .ThenByDescending(item => item.Title.EqualsIgnoreCase(query))
                                               .ThenBy(item => item.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                                               .ToList();

        aggregatedResult.Data.Results = sortedMergedResults;
        return aggregatedResult;
    }

    private static void AggregateProperties(string providerName, object? obj, Dictionary<string, string> targetDict)
    {
        if (obj == null) return;

        var properties = obj.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (!prop.CanRead) continue;
            if (prop.Name == nameof(MovieSearchResult.AggregatedData) ||
                prop.Name == nameof(WatchItem.AggregatedData) ||
                prop.Name == nameof(WatchItem.AggregatedDataJson)) continue;

            try
            {
                var val = prop.GetValue(obj);
                if (val == null) continue;

                string strVal;
                if (val is System.Collections.IEnumerable enumerable && !(val is string))
                {
                    var items = enumerable.Cast<object>().Select(i => i?.ToString()).Where(s => s.HasValue());
                    strVal = string.Join(", ", items);
                }
                else
                {
                    strVal = val.ToString() ?? string.Empty;
                }

                if (strVal.HasNoValue()) continue;

                var providerKey = $"{providerName}:{prop.Name}";
                targetDict[providerKey] = strVal;

                if (!targetDict.ContainsKey(prop.Name))
                {
                    targetDict[prop.Name] = strVal;
                }
            }
            catch
            {
                // Ignore reflection errors on individual properties
            }
        }
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

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
        var aggregatedResult = new AggregatedResult<MovieDetail>();
        var detailTasks = _providers.Select(async provider =>
        {
            try
            {
                var result = await provider.GetMovieDetailsAsync(movieId);
                return (ProviderName: provider.GetType().Name, Result: result);
            }
            catch (Exception ex)
            {
                var errorResult = new AggregatedResult<MovieDetail?>();
                errorResult.Diagnostics[provider.GetType().Name] = $"Exception: {ex.Message}";
                return (ProviderName: provider.GetType().Name, Result: errorResult);
            }
        }).ToList();

        var completedResults = await Task.WhenAll(detailTasks);
        var orderedResults = completedResults.OrderByDescending(r => r.ProviderName.Contains("Tmdb")).ToList();

        MovieDetail? mergedDetail = null;
        var aggregatedData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var completed in orderedResults)
        {
            if (completed.Result?.Diagnostics != null)
            {
                foreach (var diag in completed.Result.Diagnostics)
                {
                    aggregatedResult.Diagnostics[diag.Key] = diag.Value;
                }
            }

            var detail = completed.Result?.Data;
            if (detail == null) continue;

            var providerName = completed.ProviderName.Replace("Service", "");

            if (mergedDetail != null && detail.Title.HasValue())
            {
                var primaryNorm = NormalizeTitle(mergedDetail.Title);
                var detailNorm = NormalizeTitle(detail.Title);

                if (primaryNorm.HasValue() && detailNorm.HasValue() && !primaryNorm.Equals(detailNorm))
                {
                    aggregatedResult.Diagnostics[completed.ProviderName] = $"Skipped provider response due to title mismatch: '{detail.Title}' vs requested '{mergedDetail.Title}'.";
                    continue;
                }
            }

            AggregateProperties(providerName, detail, aggregatedData);

            if (detail.AggregatedData != null)
            {
                foreach (var kvp in detail.AggregatedData)
                {
                    aggregatedData[kvp.Key] = kvp.Value;
                }
            }

            if (mergedDetail == null)
            {
                mergedDetail = new MovieDetail
                {
                    Id = detail.Id,
                    Title = detail.Title,
                    Overview = detail.Overview,
                    PosterPath = detail.PosterPath,
                    ReleaseDate = detail.ReleaseDate,
                    Genres = detail.Genres != null ? new List<Genre>(detail.Genres) : new List<Genre>(),
                    StreamingProviders = detail.StreamingProviders != null ? new List<string>(detail.StreamingProviders) : new List<string>(),
                    PrimarySourceApi = detail.PrimarySourceApi.HasValue() ? detail.PrimarySourceApi : providerName,
                    WebUrl = detail.WebUrl,
                    MediaType = detail.MediaType
                };
            }
            else
            {
                if (mergedDetail.Title.HasNoValue() && detail.Title.HasValue()) mergedDetail.Title = detail.Title;
                if (mergedDetail.Overview.HasNoValue() && detail.Overview.HasValue()) mergedDetail.Overview = detail.Overview;
                if (mergedDetail.PosterPath.HasNoValue() && detail.PosterPath.HasValue()) mergedDetail.PosterPath = detail.PosterPath;
                if (mergedDetail.ReleaseDate.HasNoValue() && detail.ReleaseDate.HasValue()) mergedDetail.ReleaseDate = detail.ReleaseDate;
                if (mergedDetail.WebUrl.HasNoValue() && detail.WebUrl.HasValue()) mergedDetail.WebUrl = detail.WebUrl;
                if (mergedDetail.MediaType.HasNoValue() && detail.MediaType.HasValue()) mergedDetail.MediaType = detail.MediaType;

                if (detail.Genres != null)
                {
                    foreach (var genre in detail.Genres)
                    {
                        if (genre != null && mergedDetail.Genres.All(g => g.Id != genre.Id && !g.Name.EqualsIgnoreCase(genre.Name)))
                        {
                            mergedDetail.Genres.Add(genre);
                        }
                    }
                }

                if (detail.StreamingProviders != null)
                {
                    foreach (var provider in detail.StreamingProviders)
                    {
                        if (provider.HasValue() && !mergedDetail.StreamingProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
                        {
                            mergedDetail.StreamingProviders.Add(provider);
                        }
                    }
                }
            }
        }

        if (mergedDetail != null)
        {
            mergedDetail.AggregatedData = aggregatedData;
            aggregatedResult.Data = mergedDetail;
        }

        return aggregatedResult;
    }

    public async Task<AggregatedResult<MovieSearchResult>> SearchMovieAsync (string searchQuery)
    {
        var searchResponse = await SearchMoviesAsync(searchQuery);
        var firstResult = searchResponse.Data?.Results?.FirstOrDefault();

        return new AggregatedResult<MovieSearchResult>
        {
            Data = firstResult,
            Diagnostics = searchResponse.Diagnostics
        };
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
