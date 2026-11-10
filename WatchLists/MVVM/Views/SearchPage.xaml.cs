using WatchLists.Logger;
using WatchLists.MVVM.ViewModels;
using WatchLists.Services.Models;
using WatchLists.Services;

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

    private async void OpenEnhancedImport(object? sender, EventArgs eventArgs)
    {
        var service = App.Current.Services.GetRequiredService<WatchListService>();
        await Navigation.PushModalAsync(new NavigationPage(new EnhancedImportPage(service)));
    }
}
