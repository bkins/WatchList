using WatchLists.Logger;
using WatchLists.MVVM.ViewModels;
using WatchLists.Services.Models;

namespace WatchLists.MVVM.Views;

public partial class SearchPage : ContentPage
{
    public SearchPage(SearchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _ = FileLogger.WriteLogAsync($"[SearchPage] Constructor. viewModel: {viewModel?.GetHashCode()}");
    }

}
