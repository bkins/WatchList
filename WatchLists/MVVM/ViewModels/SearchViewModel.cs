using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchLists.Logger;
using WatchLists.Services;
using WatchLists.Services.Models;

namespace WatchLists.MVVM.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly MovieDataAggregator _movieDataAggregator;

    [ObservableProperty]
    private ObservableCollection<MovieSearchResult> _movies = new();

    [ObservableProperty] private string _searchQuery;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError = false;

    public SearchViewModel(MovieDataAggregator movieDataAggregator)
    {
        _movieDataAggregator = movieDataAggregator;
        _ = FileLogger.WriteLogAsync($"[SearchViewModel] Constructor. Instance: {this.GetHashCode()}");
    }

    [RelayCommand]
    public async Task Search ()
    {
        await FileLogger.WriteLogAsync($"[Search] Search command invoked. SearchQuery: '{SearchQuery}'");
        ErrorMessage = string.Empty;
        HasError     = false;

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await FileLogger.WriteLogAsync("[Search] Search query is empty or whitespace. Returning.");
            ErrorMessage = "Search query cannot be empty.";
            HasError     = true;
            return;
        }

        try
        {
            await FileLogger.WriteLogAsync($"[Search] Calling _movieDataAggregator.SearchMoviesAsync with '{SearchQuery}'");
            var results = await _movieDataAggregator.SearchMoviesAsync(SearchQuery);
            Movies.Clear();

            if (results == null)
            {
                await FileLogger.WriteLogAsync("[Search] Aggregator returned null results.");
                ErrorMessage = "Search failed: Aggregator returned null.";
                HasError     = true;
                return;
            }

            await FileLogger.WriteLogAsync($"[Search] Aggregator returned diagnostics. Count: {results.Diagnostics?.Count ?? 0}");
            if (results.Diagnostics != null)
            {
                foreach (var diag in results.Diagnostics)
                {
                    await FileLogger.WriteLogAsync($"[Search] Diagnostic: {diag.Key} => {diag.Value}");
                }
            }

            if (results.Data?.Results != null && results.Data.Results.Count > 0)
            {
                await FileLogger.WriteLogAsync($"[Search] Found {results.Data.Results.Count} movies.");
                foreach (var item in results.Data.Results)
                {
                    Movies.Add(item);
                }
            }
            else
            {
                await FileLogger.WriteLogAsync("[Search] No movie results in response data.");
                HasError = true;
                if (results.Diagnostics != null && results.Diagnostics.Count > 0)
                {
                    var errors = results.Diagnostics
                                        .Select(d => $"{d.Key}: {d.Value}")
                                        .ToList();
                    ErrorMessage = string.Join(Environment.NewLine, errors);
                }
                else
                {
                    ErrorMessage = "No movies found.";
                }
            }
        }
        catch (Exception ex)
        {
            await FileLogger.WriteLogAsync($"[Search] Exception in Search command: {ex.Message}\n{ex.StackTrace}");
            ErrorMessage = $"Error during search: {ex.Message}";
            HasError     = true;
        }
    }

    [RelayCommand]
    private async Task NavigateToDetails(int movieId)
    {
        await Shell.Current.GoToAsync($"MovieDetailsPage?movieId={movieId}");
    }
}
