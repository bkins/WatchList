using WatchLists.MVVM.ViewModels;

namespace WatchLists.MVVM.Views;

public partial class MuseImportPreviewPage : ContentPage
{
    public MuseImportPreviewPage(MuseImportPreviewViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MuseImportPreviewViewModel viewModel) await viewModel.LoadCategoriesAsync();
    }

    private async void ImportApproved(object? sender, EventArgs eventArgs)
    {
        if (BindingContext is not MuseImportPreviewViewModel viewModel) return;
        var batch = viewModel.PrepareImport();
        if (batch == null) return;

        var confirmed = await DisplayAlert("Import reviewed recommendations?"
                                         , $"Add {batch.NewItems.Count} new items and process {batch.Recommendations.Count} source links. " +
                                           "Unapproved rows will be skipped. A local database backup will be created. " +
                                           "Automatic sync must be off; older clients can overwrite recommendation sources."
                                         , "Import"
                                         , "Cancel");
        if (confirmed) await viewModel.ImportConfirmedAsync(batch);
    }

    private async void EndReview(object? sender, EventArgs eventArgs)
    {
        if (BindingContext is not MuseImportPreviewViewModel viewModel) return;
        if (await DisplayAlert("End this review?", "Discard staged approvals and suggestions for this review?", "End review", "Keep reviewing"))
            viewModel.AbandonReview();
    }
}
