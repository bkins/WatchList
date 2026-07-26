using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using WatchLists.DataAccess.Interfaces;
using WatchLists.ExtensionMethods;
using WatchLists.Services.Models;

namespace WatchLists.Services;

public class TvMazeService : IMovieDataProvider
{
    private readonly HttpClient _httpClient;
    private const    string     BaseUrl = "https://api.tvmaze.com";

    public bool IsEnabled => true;

    public TvMazeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AggregatedResult<MovieSearchResponse?>> SearchMoviesAsync(string query)
    {
        var result = new AggregatedResult<MovieSearchResponse?>();

        try
        {
            var searchUrl = $"{BaseUrl}/search/shows?q={Uri.EscapeDataString(query)}";
            var response  = await _httpClient.GetFromJsonAsync<List<TvMazeSearchResult>>(searchUrl);

            var movieResults = new List<MovieSearchResult>();

            if (response != null && response.Count > 0)
            {
                foreach (var item in response)
                {
                    if (item.Show == null) continue;

                    var rawSummary = item.Show.Summary ?? string.Empty;
                    var cleanSummary = Regex.Replace(rawSummary, "<.*?>", string.Empty);

                    var movieSearchResult = new MovieSearchResult
                                           {
                                               Id               = item.Show.Id
                                             , Title            = item.Show.Name ?? string.Empty
                                             , Overview         = cleanSummary
                                             , PosterPath       = item.Show.Image?.Medium ?? string.Empty
                                             , SourceApis       = new List<string> { "TVMaze" }
                                             , PrimarySourceApi = "TVMaze"
                                             , WebUrl           = item.Show.Url ?? $"https://www.tvmaze.com/shows/{item.Show.Id}"
                                           };

                    var providers = new List<string>();

                    if (item.Show.WebChannel?.Name.HasValue() ?? false)
                    {
                        providers.Add(item.Show.WebChannel.Name!);
                    }

                    if (item.Show.Network?.Name.HasValue() ?? false)
                    {
                        providers.Add(item.Show.Network.Name!);
                    }

                    movieSearchResult.StreamingProviders = providers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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

        try
        {
            var showUrl = $"{BaseUrl}/shows/{movieId}";
            var show    = await _httpClient.GetFromJsonAsync<TvMazeShow>(showUrl);

            if (show != null && show.Name.HasValue())
            {
                var rawSummary   = show.Summary ?? string.Empty;
                var cleanSummary = Regex.Replace(rawSummary, "<.*?>", string.Empty);

                var providers = new List<string>();
                if (show.WebChannel?.Name.HasValue() ?? false) providers.Add(show.WebChannel.Name!);
                if (show.Network?.Name.HasValue() ?? false) providers.Add(show.Network.Name!);

                result.Data = new MovieDetail
                              {
                                  Id                 = show.Id,
                                  Title              = show.Name!,
                                  Overview           = cleanSummary,
                                  PosterPath         = show.Image?.Medium ?? string.Empty,
                                  PrimarySourceApi   = "TVMaze",
                                  WebUrl             = show.Url ?? $"https://www.tvmaze.com/shows/{show.Id}",
                                  StreamingProviders = providers.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                              };
                result.Diagnostics[GetType().Name] = "Data returned successfully from TVMaze.";
            }
            else
            {
                result.Diagnostics[GetType().Name] = "No show returned from TVMaze.";
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
        var result = new AggregatedResult<WatchProvidersResponse?>();
        result.Diagnostics[GetType().Name] = "GetWatchProvidersAsync not implemented for TvMazeService.";
        return await Task.FromResult(result);
    }

    private class TvMazeSearchResult
    {
        [JsonPropertyName("show")]
        public TvMazeShow? Show { get; set; }
    }

    private class TvMazeShow
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("image")]
        public TvMazeImage? Image { get; set; }

        [JsonPropertyName("webChannel")]
        public TvMazeChannel? WebChannel { get; set; }

        [JsonPropertyName("network")]
        public TvMazeChannel? Network { get; set; }
    }

    private class TvMazeImage
    {
        [JsonPropertyName("medium")]
        public string? Medium { get; set; }
    }

    private class TvMazeChannel
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
