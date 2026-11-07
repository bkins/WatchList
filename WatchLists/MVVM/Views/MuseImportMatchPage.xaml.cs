using WatchLists.MVVM.ViewModels;

namespace WatchLists.MVVM.Views;

public partial class MuseImportMatchPage : ContentPage
{
    public MuseImportMatchPage(MuseImportPreviewViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void UseMatch(object? sender, EventArgs eventArgs)
    {
        if (BindingContext is not MuseImportPreviewViewModel viewModel) return;
        if (viewModel.SelectedExisting == null && viewModel.SelectedProvider == null)
        {
            await DisplayAlert("Choose a match", "Select an existing item or provider suggestion first.", "OK");
            return;
        }
        await Navigation.PopModalAsync();
    }
}
