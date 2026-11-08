using System.ComponentModel;
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
        if (BindingContext is MuseImportPreviewViewModel viewModel)
        {
            viewModel.PropertyChanged -= ReviewSelectionChanged;
            viewModel.PropertyChanged += ReviewSelectionChanged;
            await viewModel.LoadCategoriesAsync();
            await WizardScroll.ScrollToAsync(0, 0, false);
        }
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is MuseImportPreviewViewModel viewModel)
            viewModel.PropertyChanged -= ReviewSelectionChanged;
        base.OnDisappearing();
    }

    private async void ReviewSelectionChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MuseImportPreviewViewModel.SelectedRecommendation))
            await WizardScroll.ScrollToAsync(0, 0, false);
    }

    private async void ImportApproved(object? sender, EventArgs eventArgs)
    {
        if (BindingContext is not MuseImportPreviewViewModel viewModel) return;
        var batch = viewModel.PrepareImport();
        if (batch == null)
        {
            await DisplayAlertAsync("Cannot import yet", viewModel.Status, "OK");
            return;
        }

        var confirmed = await DisplayAlertAsync("Import reviewed recommendations?"
                                         , $"Add {batch.NewItems.Count} new items and process {batch.Recommendations.Count} source links. " +
                                           "All other rows were explicitly skipped. A local database backup will be created. " +
                                           "Cloud synchronization remains paused."
                                         , "Import"
                                         , "Cancel");
        if (confirmed) await viewModel.ImportConfirmedAsync(batch);
    }

    private async void ChooseFile(object? sender, EventArgs eventArgs)
    {
        if (BindingContext is not MuseImportPreviewViewModel viewModel || viewModel.IsBusy) return;
        if (viewModel.HasReview)
        {
            if (!await DisplayAlertAsync("Replace this review?", "Discard the current file, completed searches and decisions?", "Discard and choose file", "Keep review")) return;
            viewModel.AbandonReview();
        }
        await viewModel.OpenFileCommand.ExecuteAsync(null);
    }

    private async void ChangeMatch(object? sender, EventArgs eventArgs)
    {
        if (BindingContext is MuseImportPreviewViewModel viewModel && !viewModel.IsBusy)
            await Navigation.PushModalAsync(new NavigationPage(new MuseImportMatchPage(viewModel)));
    }

    private async void OpenReviewList(object? sender, EventArgs eventArgs)
    {
        if (BindingContext is MuseImportPreviewViewModel viewModel && !viewModel.IsBusy)
            await Navigation.PushModalAsync(new NavigationPage(new MuseImportReviewListPage(viewModel)));
    }

    private async void ShowDetails(object? sender, EventArgs eventArgs)
    {
        if (BindingContext is MuseImportPreviewViewModel viewModel)
            await DisplayAlertAsync("Recommendation details", viewModel.Details, "Close");
    }

    private async void SkipRemaining(object? sender, EventArgs eventArgs)
    {
        if (BindingContext is not MuseImportPreviewViewModel viewModel || viewModel.IsBusy) return;
        if (await DisplayAlertAsync("Skip remaining recommendations?", "Keep approved rows and explicitly skip every row that still needs a decision? You can revisit skipped rows before import.", "Skip remaining", "Keep reviewing"))
            viewModel.SkipRemaining();
    }

    private async void ReviewOptions(object? sender, EventArgs eventArgs)
    {
        if (BindingContext is not MuseImportPreviewViewModel viewModel || viewModel.IsBusy) return;
        var choice = await DisplayActionSheetAsync("Review options", "Keep reviewing", null, "Recommendation details", "Mark recommendation pending", "Keep review and leave", "Discard review");
        if (choice == "Recommendation details") ShowDetails(sender, eventArgs);
        else if (choice == "Mark recommendation pending" && viewModel.IsReviewStep) viewModel.ClearApprovalCommand.Execute(null);
        else if (choice == "Keep review and leave")
        {
            await DisplayAlertAsync("Review kept for this session", "You can return to this review while the app remains open. Closing the app loses unfinished review progress.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        else if (choice == "Discard review" && await DisplayAlertAsync("Discard review?", "Discard completed searches and staged decisions? Saved WatchList items will not change.", "Discard", "Keep review"))
            viewModel.AbandonReview();
    }

    private async void ShowBackupDetails(object? sender, EventArgs eventArgs)
    {
        if (BindingContext is MuseImportPreviewViewModel viewModel)
            await DisplayAlertAsync("Pre-import database backup", viewModel.ImportBackupPath, "Close");
    }

    private async void ViewWatchList(object? sender, EventArgs eventArgs) => await Shell.Current.GoToAsync("//MainPage");

    private void FinishReview(object? sender, EventArgs eventArgs)
    {
        if (BindingContext is MuseImportPreviewViewModel viewModel && !viewModel.IsBusy) viewModel.AbandonReview();
    }
}
