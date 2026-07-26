using WatchLists.MVVM.ViewModels;

namespace WatchLists.MVVM.Views;

public partial class MovieDetailsPage : ContentPage, IQueryAttributable
{
    public MovieDetailsPage(MovieDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public MovieDetailsPage() : this(App.Current.Services.GetRequiredService<MovieDetailsViewModel>())
    {
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is IQueryAttributable queryAttributable)
        {
            queryAttributable.ApplyQueryAttributes(query);
        }
    }
}
