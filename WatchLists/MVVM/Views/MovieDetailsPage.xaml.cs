using WatchLists.MVVM.ViewModels;
using WatchLists.Services;

namespace WatchLists.MVVM.Views;

public partial class MovieDetailsPage : ContentPage
{
    public MovieDetailsPage()
    {
        InitializeComponent();

        BindingContext = App.Current.Services.GetService<MovieDetailsViewModel>()
                      ?? new MovieDetailsViewModel(new TmdbService(new HttpClient()));
    }
}
