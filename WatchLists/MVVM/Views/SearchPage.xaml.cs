using WatchLists.Logger;
using WatchLists.MVVM.ViewModels;
using WatchLists.Services.Models;

namespace WatchLists.MVVM.Views;

public partial class SearchPage : ContentPage
{
    public SearchViewModel ViewModel { get; }

    public SearchPage(SearchViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        BindingContext = ViewModel;
        _ = FileLogger.WriteLogAsync($"[SearchPage] Constructor. viewModel: {viewModel?.GetHashCode()}");
    }

    private async void OpenMusePreview(object? sender, EventArgs eventArgs)
    {
        await Shell.Current.GoToAsync(nameof(MuseImportPreviewPage));
    }

}
