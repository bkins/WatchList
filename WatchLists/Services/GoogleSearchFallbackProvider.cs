using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WatchLists.DataAccess.Interfaces;
using WatchLists.ExtensionMethods;
using WatchLists.Services.Models;

namespace WatchLists.Services;

public class GoogleSearchFallbackProvider : IMovieDataProvider
{
    public bool IsEnabled => true;

    public async Task<AggregatedResult<MovieSearchResponse?>> SearchMoviesAsync(string query)
    {
        var result = new AggregatedResult<MovieSearchResponse?>();

        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        var googleUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query.Trim() + " movie show")}";

        var googleFallbackResult = new MovieSearchResult
                                   {
                                       Id               = Math.Abs(googleUrl.GetHashCode())
                                     , Title            = $"Search Google for \"{query.Trim()}\""
                                     , Overview         = "Tap to search Google web index for IMDb IDs, Wikipedia pages, or stream links."
                                     , PosterPath       = string.Empty
                                     , SourceApis       = new List<string> { "Google" }
                                     , PrimarySourceApi = "Google"
                                     , WebUrl           = googleUrl
                                   };

        result.Data = new MovieSearchResponse
                      {
                          Results = new List<MovieSearchResult> { googleFallbackResult }
                      };

        result.Diagnostics[GetType().Name] = "Google search fallback link generated.";
        return await Task.FromResult(result);
    }

    public async Task<AggregatedResult<MovieDetail?>> GetMovieDetailsAsync(int movieId)
    {
        var result = new AggregatedResult<MovieDetail?>();
        result.Diagnostics[GetType().Name] = "GetMovieDetailsAsync not implemented for GoogleSearchFallbackProvider.";
        return await Task.FromResult(result);
    }

    public async Task<AggregatedResult<WatchProvidersResponse?>> GetWatchProvidersAsync(int movieId)
    {
        var result = new AggregatedResult<WatchProvidersResponse?>();
        result.Diagnostics[GetType().Name] = "GetWatchProvidersAsync not implemented for GoogleSearchFallbackProvider.";
        return await Task.FromResult(result);
    }
}
