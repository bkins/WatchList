using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WatchLists.DataAccess.Interfaces;
using WatchLists.ExtensionMethods;
using WatchLists.Services.Models;

namespace WatchLists.Services;

public class WikipediaMovieProvider : IMovieDataProvider
{
    private readonly HttpClient _httpClient;
    private const string BaseApiUrl = "https://en.wikipedia.org/api/rest_v1";

    public bool IsEnabled => true;

    public WikipediaMovieProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AggregatedResult<MovieSearchResponse?>> SearchMoviesAsync(string query)
    {
        var result = new AggregatedResult<MovieSearchResponse?>();

        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        try
        {
            var movieResults = new List<MovieSearchResult>();

            // Clean title query (remove special chars for Wikipedia page lookup)
            var cleanQuery = query.Trim();

            // Try fetching page summary directly
            var summaryUrl = $"{BaseApiUrl}/page/summary/{Uri.EscapeDataString(cleanQuery)}";
            var summary = await _httpClient.GetFromJsonAsync<WikipediaPageSummary>(summaryUrl);

            if (summary != null && summary.Title.HasValue() && summary.Type != "disambiguation")
            {
                var cleanExtract = summary.Extract.HasValue() 
                    ? Regex.Replace(summary.Extract!, "<.*?>", string.Empty) 
                    : string.Empty;

                var wikipediaResult = new MovieSearchResult
                                     {
                                         Id               = Math.Abs(summary.Title!.GetHashCode())
                                       , Title            = summary.Title!
                                       , Overview         = cleanExtract
                                       , PosterPath       = summary.Thumbnail?.Source ?? string.Empty
                                       , SourceApis       = new List<string> { "Wikipedia" }
                                       , PrimarySourceApi = "Wikipedia"
                                       , WebUrl           = summary.ContentUrls?.Desktop?.Page ?? $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(summary.Title!)}"
                                     };

                movieResults.Add(wikipediaResult);
            }

            result.Data = new MovieSearchResponse
                          {
                              Results = movieResults
                          };

            result.Diagnostics[GetType().Name] = movieResults.Count > 0
                ? "Data returned successfully from Wikipedia."
                : "No matching Wikipedia article found.";
        }
        catch (Exception ex)
        {
            result.Diagnostics[GetType().Name] = $"Wikipedia search notice: {ex.Message}";
        }

        return result;
    }

    public async Task<AggregatedResult<MovieDetail?>> GetMovieDetailsAsync(int movieId)
    {
        var result = new AggregatedResult<MovieDetail?>();
        result.Diagnostics[GetType().Name] = "GetMovieDetailsAsync not implemented for WikipediaMovieProvider.";
        return await Task.FromResult(result);
    }

    public async Task<AggregatedResult<WatchProvidersResponse?>> GetWatchProvidersAsync(int movieId)
    {
        var result = new AggregatedResult<WatchProvidersResponse?>();
        result.Diagnostics[GetType().Name] = "GetWatchProvidersAsync not implemented for WikipediaMovieProvider.";
        return await Task.FromResult(result);
    }

    private class WikipediaPageSummary
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("extract")]
        public string? Extract { get; set; }

        [JsonPropertyName("thumbnail")]
        public WikipediaThumbnail? Thumbnail { get; set; }

        [JsonPropertyName("content_urls")]
        public WikipediaContentUrls? ContentUrls { get; set; }
    }

    private class WikipediaThumbnail
    {
        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }

    private class WikipediaContentUrls
    {
        [JsonPropertyName("desktop")]
        public WikipediaPageUrl? Desktop { get; set; }
    }

    private class WikipediaPageUrl
    {
        [JsonPropertyName("page")]
        public string? Page { get; set; }
    }
}
