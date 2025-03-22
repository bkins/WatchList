using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchLists.Services;
using WatchLists.Services.Models;
using WatchLists.Utilities;

namespace WatchLists.MVVM.ViewModels;

public partial class ApiTestViewModel : ObservableObject
{
    private readonly MovieDataAggregator _aggregator;

    [ObservableProperty]
    private string _selectedApiCall;

    [ObservableProperty]
    private string _queryParameter = string.Empty;

    [ObservableProperty]
    private string _resultJson = string.Empty;

    public AggregatedResult<WatchProvidersResponse>? LastDiagnosticResult { get; private set; }

    // List of API calls to choose from.
    public List<string> ApiCallOptions { get; } = new List<string>
    {
        "Search Movie (Aggregator)",
        "Get Movie Details (Aggregator)",
        "Get Movie Providers (Aggregator)",
        "Get Movie Providers Diagnostics (Aggregator)",
        "Search TV Shows (Aggregator)",
        "Get TV Details (Aggregator)",
        "Search People (Aggregator)",
        "Get Person Details (Aggregator)",
        "Get Trending Movies (Aggregator)",
        "Get Trending TV Shows (Aggregator)",
        "Get Movie Videos (Aggregator)",
        "Get TV Videos (Aggregator)"
    };

    public ApiTestViewModel(MovieDataAggregator aggregator)
    {
        _aggregator = aggregator;
        SelectedApiCall = ApiCallOptions.FirstOrDefault()!;
    }

    [RelayCommand]
    public async Task ExecuteApiCall()
    {
        try
        {
            var response = await GetApiResponse();
            ResultJson = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            ResultJson = $"Error: {ex.Message}";
        }
    }

    private async Task<object?> GetApiResponse()
    {
        return SelectedApiCall switch
               {
                       "Search Movie (Aggregator)" => await _aggregator.SearchMoviesAsync(QueryParameter)
                     , "Get Movie Details (Aggregator)" => await ApiUtility.TryParseAndExecuteAsync(QueryParameter
                                                                                                  , _aggregator.GetMovieDetailsAsync
                                                                                                  , "Movie ID")
                     , "Get Movie Providers (Aggregator)" => await ApiUtility.TryParseAndExecuteAsync(QueryParameter
                                                                                                    , _aggregator.GetWatchProvidersAsync
                                                                                                    , "Movie ID for providers")
                     , "Get Movie Providers Diagnostics (Aggregator)" => await ApiUtility.TryParseAndExecuteDiagnosticsAsync(QueryParameter
                                                                                                                           , _aggregator
                                                                                                                                     .GetWatchProvidersAsync
                                                                                                                           , "Movie ID for providers"
                                                                                                                           , _aggregator)
                     , "Search TV Shows (Aggregator)" => await _aggregator.SearchTVShowsAsync(QueryParameter)
                     , "Get TV Details (Aggregator)" => await ApiUtility.TryParseAndExecuteAsync(QueryParameter
                                                                                               , _aggregator.GetTVShowDetailsAsync
                                                                                               , "TV Show ID")
                     , "Search People (Aggregator)" => await _aggregator.SearchPeopleAsync(QueryParameter)
                     , "Get Person Details (Aggregator)" => await ApiUtility.TryParseAndExecuteAsync(QueryParameter
                                                                                                   , _aggregator.GetPersonDetailsAsync
                                                                                                   , "Person ID")
                     , "Get Trending Movies (Aggregator)" => await _aggregator.GetTrendingMoviesAsync()
                     , "Get Trending TV Shows (Aggregator)" => await _aggregator.GetTrendingTVShowsAsync()
                     , "Get Movie Videos (Aggregator)" => await ApiUtility.TryParseAndExecuteAsync(QueryParameter
                                                                                                 , _aggregator.GetMovieVideosAsync
                                                                                                 , "Movie ID for videos")
                     , "Get TV Videos (Aggregator)" => await ApiUtility.TryParseAndExecuteAsync(QueryParameter
                                                                                              , _aggregator.GetTVVideosAsync
                                                                                              , "TV Show ID for videos")
                     , _ => "Unknown API call"
               };
    }
}
