using WatchLists.MVVM.ViewModels;
using WatchLists.Services.Models;

namespace WatchLists.MVVM.Views;

public partial class SearchPage : ContentPage
{
    public SearchPage()
    {
        InitializeComponent();
        BindingContext = App.Current.Services.GetService<SearchViewModel>();
    }

    private async void OnMovieSelected(object                    sender
                                     , SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Movie selectedMovie)
        {
            await Shell.Current.GoToAsync($"MovieDetailsPage?movieId={selectedMovie.Id}");
        }
    }
}
