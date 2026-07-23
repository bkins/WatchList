using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using WatchLists.DataAccess.Interfaces;
using WatchLists.Services.Models;

namespace WatchLists.Services;

public class Imdb236Service : IMovieDataProvider
{
    private const string BaseUrl = "https://imdb236.p.rapidapi.com/api/";
    private readonly HttpClient _httpClient;

    private string _apiKey = string.Empty;
    public string ApiKey
    {
        get => _apiKey;
        set => _apiKey = value;
    }

    public bool IsEnabled { get; private set; } = true;

    public Imdb236Service(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        ApiKey      = configuration["Imdb236:ApiKey"] ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-rapidapi-key", ApiKey);
            _httpClient.DefaultRequestHeaders.Add("x-rapidapi-host", "imdb236.p.rapidapi.com");
        }
        else
        {
            Console.WriteLine("Warning: API key for Imdb236Service is not set.");
            IsEnabled = false;
        }
    }

    public async Task<AggregatedResult<MovieDetail?>> GetMovieDetailsAsync(int movieId)
    {
        var result = new AggregatedResult<MovieDetail?>();

        try
        {
            string imdbId = $"tt{movieId:D7}";
            var response = await _httpClient.GetFromJsonAsync<Imdb236MovieDetail>($"{BaseUrl}imdb/{imdbId}");

            if (response != null)
            {
                var detail = new MovieDetail
                             {
                                 Id          = movieId
                               , Title       = response.PrimaryTitle ?? response.Title ?? string.Empty
                               , Overview    = response.Description ?? response.Plot ?? string.Empty
                               , PosterPath  = response.PrimaryImage ?? response.Image ?? string.Empty
                               , Genres      = response.Genres?
                                                       .Select(genreName => new Genre
                                                                            {
                                                                                Id   = genreName.GetHashCode()
                                                                              , Name = genreName
                                                                            })
                                                       .ToList() ?? new List<Genre>()
                               , ReleaseDate = response.ReleaseDate ?? response.Year?.ToString()
                             };

                result.Data = detail;
                result.Diagnostics[GetType().Name] = "Data returned successfully.";
            }
            else
            {
                result.Diagnostics[GetType().Name] = "No data returned.";
            }
        }
        catch (Exception ex)
        {
            result.Diagnostics[GetType().Name] = $"Error: {ex.Message}";
        }

        return result;
    }

    public async Task<AggregatedResult<MovieSearchResponse?>> SearchMoviesAsync(string query)
    {
        var result = new AggregatedResult<MovieSearchResponse?>();

        try
        {
            // Autocomplete or search endpoints return titles matching the query
            var response = await _httpClient.GetFromJsonAsync<Imdb236SearchResponse>($"{BaseUrl}imdb/autocomplete?query={Uri.EscapeDataString(query)}");

            if (response?.Results != null)
            {
                var searchResults = new List<MovieSearchResult>();

                foreach (var rawResult in response.Results)
                {
                    if (rawResult == null || string.IsNullOrWhiteSpace(rawResult.Id))
                    {
                        continue;
                    }

                    // Attempt to parse standard IMDb ID (e.g. tt0816692) into integer
                    int numericId;
                    var cleanIdStr = rawResult.Id.Replace("tt", "");
                    if (!int.TryParse(cleanIdStr, out numericId))
                    {
                        numericId = rawResult.Id.GetHashCode();
                    }

                    var searchResult = new MovieSearchResult
                                       {
                                           Id         = numericId
                                         , Title      = rawResult.PrimaryTitle ?? rawResult.Title ?? string.Empty
                                         , PosterPath = rawResult.PrimaryImage ?? rawResult.Image ?? string.Empty
                                         , Overview   = rawResult.Description ?? string.Empty
                                       };

                    searchResults.Add(searchResult);
                }

                result.Data = new MovieSearchResponse
                              {
                                  Results = searchResults
                              };
                result.Diagnostics[GetType().Name] = searchResults.Count > 0
                    ? "Data returned successfully."
                    : "No data found.";
            }
            else
            {
                result.Diagnostics[GetType().Name] = "No data returned.";
            }
        }
        catch (Exception ex)
        {
            result.Diagnostics[GetType().Name] = $"Error: {ex.Message}";
        }

        return result;
    }

    public async Task<AggregatedResult<WatchProvidersResponse?>> GetWatchProvidersAsync(int movieId)
    {
        return new AggregatedResult<WatchProvidersResponse?>
               {
                   Diagnostics = { { GetType().Name, "imdb236 does not support direct watch provider lookup by movie ID." } }
               };
    }

    private class Imdb236MovieDetail
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("primaryTitle")]
        public string? PrimaryTitle { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("plot")]
        public string? Plot { get; set; }

        [JsonPropertyName("primaryImage")]
        public string? PrimaryImage { get; set; }

        [JsonPropertyName("image")]
        public string? Image { get; set; }

        [JsonPropertyName("genres")]
        public List<string>? Genres { get; set; }

        [JsonPropertyName("releaseDate")]
        public string? ReleaseDate { get; set; }

        [JsonPropertyName("year")]
        public int? Year { get; set; }
    }

    private class Imdb236SearchResponse
    {
        [JsonPropertyName("results")]
        public List<Imdb236SearchResult>? Results { get; set; }
    }

    private class Imdb236SearchResult
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("primaryTitle")]
        public string? PrimaryTitle { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("primaryImage")]
        public string? PrimaryImage { get; set; }

        [JsonPropertyName("image")]
        public string? Image { get; set; }
    }
}
