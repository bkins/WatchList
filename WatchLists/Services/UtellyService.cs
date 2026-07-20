using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WatchLists.DataAccess.Interfaces;
using WatchLists.ExtensionMethods;
using WatchLists.Services.Models;

namespace WatchLists.Services;

public class UtellyService : IMovieDataProvider
{
    private const string BaseUrl = "https://utelly-tv-shows-and-movies-availability-v1.p.rapidapi.com/";
    private readonly HttpClient _httpClient;

    public string ApiKey { get; set; }
    public bool IsEnabled { get; private set; } = true;

    public UtellyService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        ApiKey      = configuration["Utelly:ApiKey"];

        if (ApiKey != null
         && ApiKey.HasValue())
        {
            _httpClient.DefaultRequestHeaders.Add("X-RapidAPI-Key", ApiKey);
            _httpClient.DefaultRequestHeaders.Add("X-RapidAPI-Host", "utelly-tv-shows-and-movies-availability-v1.p.rapidapi.com");
        }
        else
        {
            Console.WriteLine("Warning: API key for UtellyService is not set.");
            IsEnabled = false;
        }
    }

    public async Task<AggregatedResult<MovieDetail>> GetMovieDetailsAsync(int movieId)
    {
        var result = new AggregatedResult<MovieDetail>
        {
            Diagnostics = { { GetType().Name, "Utelly does not support movie details retrieval." } }
        };

        return result;
    }

    public async Task<AggregatedResult<MovieSearchResponse>> SearchMoviesAsync(string query)
    {
        var result = new AggregatedResult<MovieSearchResponse>();

        try
        {
            var response = await FetchFromUtelly<UtellySearchResponse>($"lookup?term={Uri.EscapeDataString(query)}&country=US");

            if (response?.Results == null)
            {
                throw new NullReferenceException($"{nameof(response)} is null");
            }

            var movieResults = new List<MovieSearchResult>();
            foreach (var utellyResult in response.Results)
            {
                var providerNames = utellyResult.Providers?
                                                .Select(provider => provider.ProviderName)
                                                .Where(name => !string.IsNullOrWhiteSpace(name))
                                                .ToList() ?? new List<string>();

                var movieSearchResult = new MovieSearchResult
                                        {
                                            Id                 = utellyResult.Title.GetHashCode()
                                          , Title              = utellyResult.Title
                                          , PosterPath         = string.Empty
                                          , Overview           = providerNames.Any()
                                                                    ? $"Available on: {string.Join(", ", providerNames)}"
                                                                    : "Streaming availability unknown."
                                          , StreamingProviders = providerNames
                                        };
                movieResults.Add(movieSearchResult);
            }

            result.Data = new MovieSearchResponse
                          {
                              Results = movieResults
                          };
            result.Diagnostics[GetType().Name] = movieResults.Count > 0
                                                    ? "Data returned successfully."
                                                    : "No data found.";
        }
        catch (Exception ex)
        {
            result.Diagnostics[GetType().Name] = $"Error: {ex.Message}";
        }

        return result;
    }

    public async Task<AggregatedResult<WatchProvidersResponse>> GetWatchProvidersAsync(int movieId)
    {
        var result = new AggregatedResult<WatchProvidersResponse>
        {
            Diagnostics = { { GetType().Name, "Utelly does not support direct watch provider lookup by movie ID." } }
        };

        return result;
    }

    private async Task<T?> FetchFromUtelly<T>(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}{endpoint}");

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Utelly API error: HTTP {response.StatusCode}");
                return default;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Utelly API error: {ex.Message}");
            return default;
        }
    }
}
