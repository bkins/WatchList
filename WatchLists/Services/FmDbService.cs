using System.Net.Http.Json;
using System.Text.Json.Serialization;
using WatchLists.DataAccess.Interfaces;
using WatchLists.ExtensionMethods;
using WatchLists.Services.Models;

namespace WatchLists.Services;

public class FmDbService : IMovieDataProvider
{
    private readonly HttpClient _httpClient;
    private const    string     BaseUrl = "https://imdb.iamidiotareyoutoo.com";

    public bool IsEnabled => true;

    public FmDbService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AggregatedResult<MovieSearchResponse?>> SearchMoviesAsync(string query)
    {
        var result = new AggregatedResult<MovieSearchResponse?>();

        try
        {
            var searchUrl = $"{BaseUrl}/search?q={Uri.EscapeDataString(query)}";
            var response  = await _httpClient.GetFromJsonAsync<FmDbSearchResponse>(searchUrl);

            var movieResults = new List<MovieSearchResult>();

            if (response?.Description != null && response.Description.Count > 0)
            {
                foreach (var item in response.Description)
                {
                    if (item.Title.HasNoValue()) continue;

                    var imdbIdHash = item.ImdbId.HasValue() ? item.ImdbId!.GetHashCode() : item.Title!.GetHashCode();
                    var movieId    = Math.Abs(imdbIdHash);

                    var overviewText = item.Actors.HasValue()
                        ? $"Year: {item.Year?.ToString() ?? "N/A"} | Cast: {item.Actors}"
                        : $"Year: {item.Year?.ToString() ?? "N/A"}";

                    var movieSearchResult = new MovieSearchResult
                                           {
                                               Id               = movieId
                                             , Title            = item.Title!
                                             , Overview         = overviewText
                                             , PosterPath       = item.PosterUrl ?? string.Empty
                                             , SourceApis       = new List<string> { "FM-DB" }
                                             , PrimarySourceApi = "FM-DB"
                                             , WebUrl           = item.ImdbUrl ?? $"https://www.imdb.com/title/{item.ImdbId}/"
                                           };

                    movieResults.Add(movieSearchResult);
                }
            }

            result.Data = new MovieSearchResponse
                          {
                              Results = movieResults
                          };

            result.Diagnostics[GetType().Name] = movieResults.Count > 0
                ? "Data returned successfully from FM-DB."
                : "No data returned from FM-DB.";
        }
        catch (Exception ex)
        {
            result.Diagnostics[GetType().Name] = $"Error: {ex.Message}";
        }

        return result;
    }

    public async Task<AggregatedResult<MovieDetail?>> GetMovieDetailsAsync(int movieId)
    {
        var result = new AggregatedResult<MovieDetail?>();
        result.Diagnostics[GetType().Name] = "FM-DB movie details retrieval by internal ID not directly supported.";
        return await Task.FromResult(result);
    }

    public async Task<AggregatedResult<WatchProvidersResponse?>> GetWatchProvidersAsync(int movieId)
    {
        var result = new AggregatedResult<WatchProvidersResponse?>();
        result.Diagnostics[GetType().Name] = "GetWatchProvidersAsync not implemented for FmDbService.";
        return await Task.FromResult(result);
    }

    private class FmDbSearchResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("description")]
        public List<FmDbSearchItem>? Description { get; set; }
    }

    private class FmDbSearchItem
    {
        [JsonPropertyName("#TITLE")]
        public string? Title { get; set; }

        [JsonPropertyName("#YEAR")]
        public int? Year { get; set; }

        [JsonPropertyName("#IMDB_ID")]
        public string? ImdbId { get; set; }

        [JsonPropertyName("#ACTORS")]
        public string? Actors { get; set; }

        [JsonPropertyName("#IMDB_URL")]
        public string? ImdbUrl { get; set; }

        [JsonPropertyName("#IMG_POSTER")]
        public string? PosterUrl { get; set; }
    }
}
