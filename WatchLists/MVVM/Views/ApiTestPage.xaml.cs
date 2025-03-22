using WatchLists.MVVM.ViewModels;
using WatchLists.Services;

namespace WatchLists.MVVM.Views;

public partial class ApiTestPage : ContentPage
{
    public ApiTestPage()
    {
        InitializeComponent();

        BindingContext = App.Current
                            .Services
                            .GetService<ApiTestViewModel>()
                      ?? new ApiTestViewModel(App.Current
                                                 .Services
                                                 .GetService<MovieDataAggregator>());
    }
}
