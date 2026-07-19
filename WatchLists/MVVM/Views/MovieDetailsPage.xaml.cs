using WatchLists.MVVM.ViewModels;
using WatchLists.Services;

namespace WatchLists.MVVM.Views;

public partial class MovieDetailsPage : ContentPage, IQueryAttributable
{
    public MovieDetailsPage()
    {
        InitializeComponent();

        BindingContext = App.Current.Services.GetService<MovieDetailsViewModel>()
                      ?? new MovieDetailsViewModel(new TmdbService(new HttpClient(), null));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is IQueryAttributable queryAttributable)
        {
            queryAttributable.ApplyQueryAttributes(query);
        }
    }
}
