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
            await DisplayAlertAsync("Choose a match", "Select an existing item or provider suggestion first.", "OK");
            return;
        }
        await Navigation.PopModalAsync();
    }

    private async void BackToReview(object? sender, EventArgs eventArgs)
    {
        await Navigation.PopModalAsync();
    }

    private async void SkipRecommendation(object? sender, EventArgs eventArgs)
    {
        if (BindingContext is not MuseImportPreviewViewModel viewModel || !viewModel.CanChangeReview) return;
        viewModel.SkipAndNextCommand.Execute(null);
        await Navigation.PopModalAsync();
    }
}
