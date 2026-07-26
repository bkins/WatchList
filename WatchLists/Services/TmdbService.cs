using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using WatchLists.DataAccess.Interfaces;
using WatchLists.ExtensionMethods;
using WatchLists.Services.Models;

namespace WatchLists.Services;

public class TmdbService : IMovieDataProvider
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private const string ImageBaseUrl = "https://image.tmdb.org/t/p/w500";

    private string _apiKey;
    public string ApiKey
    {
        get => _apiKey;
        set
        {
            Console.WriteLine($"ApiKey set: {value}");
            _apiKey = value;
        }
    }

    public bool IsEnabled { get; private set; } = true;

    public TmdbService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        ApiKey      = configuration["TMDb:ApiKey"];
        Console.WriteLine($"API Key: {ApiKey}");
        Console.WriteLine($"TmdbService Instance: {this.GetHashCode()} - ApiKey: {ApiKey}");
    }

    #region Common API Methods

    public async Task<AggregatedResult<MovieDetail>> GetMovieDetailsAsync(int movieId)
    {
        var result = new AggregatedResult<MovieDetail>();

        try
        {
            var url = $"{BaseUrl}/movie/{movieId}?api_key={ApiKey}";
            var response = await _httpClient.GetFromJsonAsync<MovieDetail>(url);

            if (response != null && response.Title.HasValue())
            {
                result.Data = response;
                result.Diagnostics[GetType().Name] = "Data returned successfully from movie endpoint.";
            }
        }
        catch
        {
            // /movie/ endpoint failed, proceed to /tv/ fallback
        }

        if (result.Data == null || result.Data.Title.HasNoValue())
        {
            try
            {
                var tvUrl = $"{BaseUrl}/tv/{movieId}?api_key={ApiKey}";
                var tvResponse = await _httpClient.GetFromJsonAsync<TmdbTvDetail>(tvUrl);

                if (tvResponse != null && tvResponse.Name.HasValue())
                {
                    result.Data = new MovieDetail
                    {
                        Id         = tvResponse.Id,
                        Title      = tvResponse.Name,
                        Overview   = tvResponse.Overview,
                        PosterPath = tvResponse.PosterPath
                    };
                    result.Diagnostics[GetType().Name] = "Data returned successfully from TV endpoint.";
                }
                else
                {
                    result.Diagnostics[GetType().Name] = "No data returned from movie or TV endpoints.";
                }
            }
            catch (Exception ex)
            {
                result.Diagnostics[GetType().Name] = $"Error: {ex.Message}";
            }
        }

        return result;
    }

    #endregion

    #region Movies

    public async Task<AggregatedResult<MovieSearchResponse>> SearchMoviesAsync(string query)
    {
        var result = new AggregatedResult<MovieSearchResponse>();

        try
        {
            var url = $"{BaseUrl}/search/movie?api_key={ApiKey}&query={Uri.EscapeDataString(query)}";
            var response = await _httpClient.GetFromJsonAsync<MovieSearchResponse>(url);

            if (response?.Results != null && response.Results.Count > 0)
            {
                var providerTasks = response.Results.Select(async movie =>
                {
                    var providersResult = await GetWatchProvidersAsync(movie.Id);
                    if (providersResult.Data?.Results != null
                     && providersResult.Data.Results.TryGetValue("US", out var usProviders))
                    {
                        var streamingNames = usProviders.Flatrate
                                                        ?.Where(watchProvider => watchProvider.ProviderName.HasValue())
                                                        .Select(watchProvider => watchProvider.ProviderName)
                                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                                        .ToList();

                        if (streamingNames != null && streamingNames.Count > 0)
                        {
                            movie.StreamingProviders = streamingNames;
                        }
                    }
                    movie.SourceApis = new List<string> { "TMDB" };
                    movie.PrimarySourceApi = "TMDB";
                    movie.WebUrl = $"https://www.themoviedb.org/movie/{movie.Id}";
                }).ToList();

                await Task.WhenAll(providerTasks);
            }

            result.Data = response;
            result.Diagnostics[GetType().Name] = response != null && response.Results.Count > 0
                ? "Data returned successfully."
                : "No data returned.";
        }
        catch (Exception ex)
        {
            result.Diagnostics[GetType().Name] = $"Error: {ex.Message}";
        }

        return result;
    }

    public async Task<AggregatedResult<TrendingResult>> GetTrendingMoviesAsync(string timeWindow = "week", int page = 1)
    {
        var result = new AggregatedResult<TrendingResult>();

        try
        {
            var url = $"{BaseUrl}/trending/movie/{timeWindow}?api_key={ApiKey}&page={page}";
            var response = await _httpClient.GetFromJsonAsync<TrendingResult>(url);

            result.Data = response;
            result.Diagnostics[GetType().Name] = response != null ? "Data returned successfully." : "No data returned.";
        }
        catch (Exception ex)
        {
            result.Diagnostics[GetType().Name] = $"Error: {ex.Message}";
        }

        return result;
    }

    #endregion

    #region Watch Providers

    public async Task<AggregatedResult<WatchProvidersResponse>> GetWatchProvidersAsync(int movieId)
    {
        var result = new AggregatedResult<WatchProvidersResponse>();

        try
        {
            var url = $"{BaseUrl}/movie/{movieId}/watch/providers?api_key={ApiKey}";
            var response = await _httpClient.GetFromJsonAsync<WatchProvidersResponse>(url);
            var filtered = FilterEmptyProviders(response);

            if (filtered.Results != null && filtered.Results.Count > 0)
            {
                result.Data = filtered;
                result.Diagnostics[GetType().Name] = "Data returned successfully from movie watch providers.";
            }
        }
        catch
        {
            // /movie/ endpoint failed, proceed to /tv/ fallback
        }

        if (result.Data == null || result.Data.Results == null || result.Data.Results.Count == 0)
        {
            try
            {
                var tvUrl = $"{BaseUrl}/tv/{movieId}/watch/providers?api_key={ApiKey}";
                var tvResponse = await _httpClient.GetFromJsonAsync<WatchProvidersResponse>(tvUrl);
                var filteredTv = FilterEmptyProviders(tvResponse);

                if (filteredTv.Results != null && filteredTv.Results.Count > 0)
                {
                    result.Data = filteredTv;
                    result.Diagnostics[GetType().Name] = "Data returned successfully from TV watch providers.";
                }
                else
                {
                    result.Diagnostics[GetType().Name] = "No streaming providers found.";
                }
            }
            catch (Exception ex)
            {
                result.Diagnostics[GetType().Name] = $"Error: {ex.Message}";
            }
        }

        return result;
    }

    private WatchProvidersResponse FilterEmptyProviders(WatchProvidersResponse? providers)
    {
        if (providers?.Results == null)
            return new WatchProvidersResponse { Results = new Dictionary<string, CountryWatchProviders>() };

        var filteredResults = new Dictionary<string, CountryWatchProviders>();

        foreach (var entry in providers.Results)
        {
            var countryProviders = entry.Value;

            if ((countryProviders.Flatrate
                                 ?.Any(watchProviders => watchProviders.ProviderName
                                                                       .HasValue()) ?? false)
             || (countryProviders.Rent
                                 ?.Any(watchProviders => watchProviders.ProviderName
                                                                       .HasValue()) ?? false)
             || (countryProviders.Buy
                                 ?.Any(watchProviders => watchProviders.ProviderName
                                                                       .HasValue()) ?? false))
            {
                filteredResults[entry.Key] = countryProviders;
            }
        }

        return new WatchProvidersResponse { Results = filteredResults };
    }

    #endregion
}
