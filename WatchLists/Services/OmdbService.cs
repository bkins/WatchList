using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using WatchLists.DataAccess.Interfaces;
using WatchLists.ExtensionMethods;
using WatchLists.Services.Models;

namespace WatchLists.Services;

public class OmdbService : IMovieDataProvider
{
    private readonly HttpClient     _httpClient;
    private const    string         BaseUrl = "https://www.omdbapi.com/";
    private readonly string?        _apiKey;

    public bool IsEnabled => _apiKey.HasValue();

    public OmdbService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey     = configuration["OMDB:ApiKey"];
    }

    public async Task<AggregatedResult<MovieSearchResponse?>> SearchMoviesAsync(string query)
    {
        var result = new AggregatedResult<MovieSearchResponse?>();

        if (_apiKey.HasNoValue())
        {
            result.Diagnostics[GetType().Name] = "Warning: OMDB API Key is missing in configuration.";
            return result;
        }

        try
        {
            var isImdbId = System.Text.RegularExpressions.Regex.IsMatch(query.Trim(), @"^tt\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var movieResults = new List<MovieSearchResult>();

            if (isImdbId)
            {
                var directUrl = $"{BaseUrl}?apikey={_apiKey}&i={Uri.EscapeDataString(query.Trim())}";
                var directItem = await _httpClient.GetFromJsonAsync<OmdbItemDetail>(directUrl);

                if (directItem != null && directItem.Response.EqualsIgnoreCase("True") && directItem.Title.HasValue())
                {
                    var poster = (directItem.Poster.HasValue() && !directItem.Poster!.EqualsIgnoreCase("N/A"))
                        ? directItem.Poster!
                        : string.Empty;

                    var imdbIdHash = directItem.ImdbId.HasValue() ? directItem.ImdbId!.GetHashCode() : directItem.Title!.GetHashCode();
                    var movieId    = Math.Abs(imdbIdHash);

                    var movieSearchResult = new MovieSearchResult
                                           {
                                               Id               = movieId
                                             , Title            = directItem.Title!
                                             , Overview         = directItem.Plot.HasValue() && !directItem.Plot!.EqualsIgnoreCase("N/A") 
                                                     ? directItem.Plot! 
                                                     : $"Year: {directItem.Year ?? "N/A"}"
                                             , PosterPath       = poster
                                             , MediaType        = directItem.Type.EqualsIgnoreCase("series") ? "Show" : "Movie"
                                             , SourceApis       = new List<string> { "OMDB" }
                                             , PrimarySourceApi = "OMDB"
                                             , WebUrl           = directItem.ImdbId.HasValue()
                                                     ? $"https://www.imdb.com/title/{directItem.ImdbId}/"
                                                     : $"https://www.omdbapi.com/?t={Uri.EscapeDataString(directItem.Title!)}"
                                           };

                    movieResults.Add(movieSearchResult);
                }
            }
            else
            {
                var searchUrl = $"{BaseUrl}?apikey={_apiKey}&s={Uri.EscapeDataString(query)}";
                var response  = await _httpClient.GetFromJsonAsync<OmdbSearchResponse>(searchUrl);

                if (response?.Search != null && response.Search.Count > 0)
                {
                    foreach (var item in response.Search)
                    {
                        if (item.Title.HasNoValue()) continue;

                        var poster = (item.Poster.HasValue() && !item.Poster!.EqualsIgnoreCase("N/A"))
                            ? item.Poster!
                            : string.Empty;

                        var imdbIdHash = item.ImdbId.HasValue() ? item.ImdbId!.GetHashCode() : item.Title!.GetHashCode();
                        var movieId    = Math.Abs(imdbIdHash);

                        var movieSearchResult = new MovieSearchResult
                                               {
                                                   Id               = movieId
                                                 , Title            = item.Title!
                                                 , Overview         = $"Year: {item.Year ?? "N/A"}"
                                                 , PosterPath       = poster
                                                 , SourceApis       = new List<string> { "OMDB" }
                                                 , PrimarySourceApi = "OMDB"
                                                 , WebUrl           = item.ImdbId.HasValue()
                                                         ? $"https://www.imdb.com/title/{item.ImdbId}/"
                                                         : $"https://www.omdbapi.com/?t={Uri.EscapeDataString(item.Title!)}"
                                               };

                        movieResults.Add(movieSearchResult);
                    }
                }
            }

            result.Data = new MovieSearchResponse
                          {
                              Results = movieResults
                          };

            result.Diagnostics[GetType().Name] = movieResults.Count > 0
                ? "Data returned successfully from OMDB."
                : "No data returned from OMDB.";
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

        if (_apiKey.HasNoValue())
        {
            result.Diagnostics[GetType().Name] = "Warning: OMDB API Key is missing in configuration.";
            return result;
        }

        result.Diagnostics[GetType().Name] = "OMDB movie details retrieval by internal ID not directly supported.";
        return await Task.FromResult(result);
    }

    public async Task<AggregatedResult<WatchProvidersResponse?>> GetWatchProvidersAsync(int movieId)
    {
        var result = new AggregatedResult<WatchProvidersResponse?>();
        result.Diagnostics[GetType().Name] = "GetWatchProvidersAsync not implemented for OmdbService.";
        return await Task.FromResult(result);
    }

    private class OmdbSearchResponse
    {
        [JsonPropertyName("Search")]
        public List<OmdbSearchItem>? Search { get; set; }

        [JsonPropertyName("totalResults")]
        public string? TotalResults { get; set; }

        [JsonPropertyName("Response")]
        public string? Response { get; set; }
    }

    private class OmdbSearchItem
    {
        [JsonPropertyName("Title")]
        public string? Title { get; set; }

        [JsonPropertyName("Year")]
        public string? Year { get; set; }

        [JsonPropertyName("imdbID")]
        public string? ImdbId { get; set; }

        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Poster")]
        public string? Poster { get; set; }
    }

    private class OmdbItemDetail
    {
        [JsonPropertyName("Title")]
        public string? Title { get; set; }

        [JsonPropertyName("Year")]
        public string? Year { get; set; }

        [JsonPropertyName("Plot")]
        public string? Plot { get; set; }

        [JsonPropertyName("imdbID")]
        public string? ImdbId { get; set; }

        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Poster")]
        public string? Poster { get; set; }

        [JsonPropertyName("Response")]
        public string? Response { get; set; }
    }
}
