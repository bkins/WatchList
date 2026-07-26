using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using WatchLists.DataAccess.Interfaces;
using WatchLists.ExtensionMethods;
using WatchLists.Services.Models;

namespace WatchLists.Services;

public class WatchmodeService : IMovieDataProvider
{
    private readonly HttpClient    _httpClient;
    private const    string        BaseUrl = "https://api.watchmode.com/v1";

    public string ApiKey    { get; set; }
    public bool   IsEnabled => ApiKey.HasValue();

    public WatchmodeService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        ApiKey      = configuration["WatchMode:ApiKey"] ?? string.Empty;
    }

    public async Task<AggregatedResult<MovieSearchResponse?>> SearchMoviesAsync(string query)
    {
        var result = new AggregatedResult<MovieSearchResponse?>();

        if (ApiKey.HasNoValue())
        {
            result.Diagnostics[GetType().Name] = "Disabled: WatchMode API key missing.";
            return result;
        }

        try
        {
            var searchUrl = $"{BaseUrl}/search/?apiKey={ApiKey}&search_field=name&search_value={Uri.EscapeDataString(query)}&types=movie";
            var response  = await _httpClient.GetFromJsonAsync<WatchmodeSearchResponse>(searchUrl);

            var movieResults = new List<MovieSearchResult>();

            if (response?.TitleResults != null && response.TitleResults.Count > 0)
            {
                var tasks = response.TitleResults.Select(async titleResult =>
                {
                    var movieSearchResult = new MovieSearchResult
                                           {
                                               Id         = titleResult.Id
                                             , Title      = titleResult.Name ?? string.Empty
                                             , Overview   = $"Release Year: {titleResult.Year}"
                                             , SourceApis = new List<string> { "Watchmode" }
                                           };

                    try
                    {
                        var sourcesUrl = $"{BaseUrl}/title/{titleResult.Id}/sources/?apiKey={ApiKey}";
                        var sources    = await _httpClient.GetFromJsonAsync<List<WatchmodeSource>>(sourcesUrl);

                        if (sources != null && sources.Count > 0)
                        {
                            var providerNames = sources.Where(source => source.Type.EqualsIgnoreCase("sub") || source.Type.EqualsIgnoreCase("free"))
                                                       .Select(source => source.Name)
                                                       .Where(name => name.HasValue())
                                                       .Distinct(StringComparer.OrdinalIgnoreCase)
                                                       .ToList();

                            movieSearchResult.StreamingProviders = providerNames;
                        }
                    }
                    catch
                    {
                        // Ignore individual title sources failure
                    }

                    return movieSearchResult;
                }).ToList();

                var processedResults = await Task.WhenAll(tasks);
                movieResults.AddRange(processedResults);
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
        result.Diagnostics[GetType().Name] = "GetMovieDetailsAsync not implemented for WatchmodeService.";
        return await Task.FromResult(result);
    }

    public async Task<AggregatedResult<WatchProvidersResponse?>> GetWatchProvidersAsync(int movieId)
    {
        var result = new AggregatedResult<WatchProvidersResponse?>();

        if (ApiKey.HasNoValue())
        {
            result.Diagnostics[GetType().Name] = "Disabled: WatchMode API key missing.";
            return result;
        }

        try
        {
            var sourcesUrl = $"{BaseUrl}/title/{movieId}/sources/?apiKey={ApiKey}";
            var sources    = await _httpClient.GetFromJsonAsync<List<WatchmodeSource>>(sourcesUrl);

            var providersList = sources?.Where(source => source.Name.HasValue())
                                        .Select(source => new WatchProviders { ProviderName = source.Name! })
                                        .ToList() ?? new List<WatchProviders>();

            var countryProviders = new CountryWatchProviders
                                   {
                                       Flatrate = providersList
                                   };

            result.Data = new WatchProvidersResponse
                          {
                              Results = new Dictionary<string, CountryWatchProviders>
                                        {
                                            { "US", countryProviders }
                                        }
                          };

            result.Diagnostics[GetType().Name] = "Data returned successfully.";
        }
        catch (Exception ex)
        {
            result.Diagnostics[GetType().Name] = $"Error: {ex.Message}";
        }

        return result;
    }

    private class WatchmodeSearchResponse
    {
        [JsonPropertyName("title_results")]
        public List<WatchmodeTitleResult>? TitleResults { get; set; }
    }

    private class WatchmodeTitleResult
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("year")]
        public int? Year { get; set; }
    }

    private class WatchmodeSource
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("region")]
        public string? Region { get; set; }
    }
}
