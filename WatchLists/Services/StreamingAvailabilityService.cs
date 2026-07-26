using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using WatchLists.DataAccess.Interfaces;
using WatchLists.ExtensionMethods;
using WatchLists.Services.Models;

namespace WatchLists.Services;

public class StreamingAvailabilityService : IMovieDataProvider
{
    private readonly HttpClient _httpClient;
    private const    string     Host = "streaming-availability.p.rapidapi.com";
    private const    string     BaseUrl = "https://streaming-availability.p.rapidapi.com/shows/search/title";

    public string ApiKey    { get; set; }
    public bool   IsEnabled => ApiKey.HasValue();

    public StreamingAvailabilityService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        ApiKey      = configuration["RapidAPI:ApiKey"] ?? string.Empty;
    }

    public async Task<AggregatedResult<MovieSearchResponse?>> SearchMoviesAsync(string query)
    {
        var result = new AggregatedResult<MovieSearchResponse?>();

        if (ApiKey.HasNoValue())
        {
            result.Diagnostics[GetType().Name] = "Disabled: RapidAPI key missing.";
            return result;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}?title={Uri.EscapeDataString(query)}&country=us&show_type=movie");
            request.Headers.Add("x-rapidapi-key", ApiKey);
            request.Headers.Add("x-rapidapi-host", Host);

            var httpResponse = await _httpClient.SendAsync(request);
            httpResponse.EnsureSuccessStatusCode();

            var shows = await httpResponse.Content.ReadFromJsonAsync<List<RapidApiShowResult>>();

            var movieResults = new List<MovieSearchResult>();

            if (shows != null && shows.Count > 0)
            {
                foreach (var show in shows)
                {
                    var movieSearchResult = new MovieSearchResult
                                           {
                                               Title      = show.Title ?? string.Empty
                                             , Overview   = show.Overview ?? string.Empty
                                             , PosterPath = show.ImageSet?.VerticalPoster?.W480 ?? string.Empty
                                           };

                    if (show.StreamingOptions != null
                     && show.StreamingOptions.TryGetValue("us", out var usOptions)
                     && usOptions != null)
                    {
                        var providerNames = usOptions.Where(option => option.Service?.Name.HasValue() ?? false)
                                                     .Where(option => option.Type.HasNoValue() || option.Type.EqualsIgnoreCase("subscription") || option.Type.EqualsIgnoreCase("free"))
                                                     .Select(option => option.Service!.Name!)
                                                     .Distinct(StringComparer.OrdinalIgnoreCase)
                                                     .ToList();

                        movieSearchResult.StreamingProviders = providerNames;
                    }

                    movieResults.Add(movieSearchResult);
                }
            }

            result.Data = new MovieSearchResponse
                          {
                              Results = movieResults
                          };

            result.Diagnostics[GetType().Name] = movieResults.Count > 0
                ? "Data returned successfully."
                : "No data returned.";
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
        result.Diagnostics[GetType().Name] = "GetMovieDetailsAsync not implemented for StreamingAvailabilityService.";
        return await Task.FromResult(result);
    }

    public async Task<AggregatedResult<WatchProvidersResponse?>> GetWatchProvidersAsync(int movieId)
    {
        var result = new AggregatedResult<WatchProvidersResponse?>();
        result.Diagnostics[GetType().Name] = "GetWatchProvidersAsync not implemented for StreamingAvailabilityService.";
        return await Task.FromResult(result);
    }

    private class RapidApiShowResult
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("overview")]
        public string? Overview { get; set; }

        [JsonPropertyName("imageSet")]
        public RapidApiImageSet? ImageSet { get; set; }

        [JsonPropertyName("streamingOptions")]
        public Dictionary<string, List<RapidApiStreamingOption>>? StreamingOptions { get; set; }
    }

    private class RapidApiImageSet
    {
        [JsonPropertyName("verticalPoster")]
        public RapidApiImageWidths? VerticalPoster { get; set; }
    }

    private class RapidApiImageWidths
    {
        [JsonPropertyName("w480")]
        public string? W480 { get; set; }
    }

    private class RapidApiStreamingOption
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("service")]
        public RapidApiServiceInfo? Service { get; set; }
    }

    private class RapidApiServiceInfo
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
