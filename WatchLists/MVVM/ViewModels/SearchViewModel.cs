using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchLists.Services;
using WatchLists.Services.Models;

namespace WatchLists.MVVM.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly MovieDataAggregator _movieDataAggregator;

    public AggregatedResult<MovieSearchResponse> Movies { get; set; } = new();

    [ObservableProperty] private string _searchQuery;

    public SearchViewModel(MovieDataAggregator movieDataAggregator)
    {
        _movieDataAggregator = movieDataAggregator;
    }

    [RelayCommand]
    public async Task Search (string query)
    {
        var results = await _movieDataAggregator.SearchMoviesAsync(query);
        // Movies = new ObservableCollection<MovieSearchResponse>(results);
        Movies = results;
    }

    [RelayCommand]
    private async Task NavigateToDetails(int movieId)
    {
        await Shell.Current.GoToAsync($"MovieDetailsPage?movieId={movieId}");
    }
}
