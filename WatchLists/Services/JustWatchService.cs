using System.Text.Json;
using WatchLists.DataAccess.Interfaces;
using WatchLists.Services.Models;

namespace WatchLists.Services;

public class JustWatchService : IMovieDataProvider
{
    private const string BaseUrl = "https://apis.justwatch.com/content/";
    private readonly HttpClient _httpClient;

    public string ApiKey { get; set; }
    public bool IsEnabled { get; private set; } = true;

    public JustWatchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AggregatedResult<MovieDetail>> GetMovieDetailsAsync(int movieId)
    {
        var result = new AggregatedResult<MovieDetail>();

        try
        {
            var response = await FetchFromJustWatch<MovieDetail>($"movies/{movieId}");

            result.Data = response;
            result.Diagnostics[GetType().Name] = response != null
                ? "Data returned successfully."
                : "No data found.";
        }
        catch (Exception ex)
        {
            result.Diagnostics[GetType().Name] = $"Error: {ex.Message}";
        }

        return result;
    }

    public async Task<AggregatedResult<MovieSearchResponse>> SearchMoviesAsync(string query)
    {
        var result = new AggregatedResult<MovieSearchResponse>();

        try
        {
            var response = await FetchFromJustWatch<MovieSearchResponse>($"search?query={query}");

            result.Data = response;
            result.Diagnostics[GetType().Name] = response != null && response.Results.Count > 0
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
        var result = new AggregatedResult<WatchProvidersResponse>();

        try
        {
            var response = await FetchFromJustWatch<WatchProvidersResponse>($"movies/{movieId}/providers");

            result.Data = response;
            result.Diagnostics[GetType().Name] = response != null && response.Results.Count > 0
                ? "Data returned successfully."
                : "No watch providers found.";
        }
        catch (Exception ex)
        {
            result.Diagnostics[GetType().Name] = $"Error: {ex.Message}";
        }

        return result;
    }

    private async Task<T?> FetchFromJustWatch<T>(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}{endpoint}");

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"JustWatch API error: HTTP {response.StatusCode}");
                return default;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"JustWatch API error: {ex.Message}");
            return default;
        }
    }
}
