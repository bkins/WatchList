using WatchLists.MVVM.ViewModels;
using WatchLists.Services;
using WatchLists.Services.Interfaces;
using WatchLists.DataAccess.Interfaces;

namespace WatchLists.MVVM.Views;

public partial class MovieDetailsPage : ContentPage, IQueryAttributable
{
    public MovieDetailsPage()
    {
        InitializeComponent();

        var services = App.Current.Services;
        BindingContext = services.GetService<MovieDetailsViewModel>()
                      ?? new MovieDetailsViewModel(services.GetService<IMovieDataAggregator>() 
                                                   ?? new MovieDataAggregator(System.Linq.Enumerable.Empty<IMovieDataProvider>()));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is IQueryAttributable queryAttributable)
        {
            queryAttributable.ApplyQueryAttributes(query);
        }
    }
}
