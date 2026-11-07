using WatchLists.MVVM.ViewModels;

namespace WatchLists.MVVM.Views;

public partial class MuseImportReviewListPage : ContentPage
{
    private readonly MuseImportPreviewViewModel _viewModel;

    public MuseImportReviewListPage(MuseImportPreviewViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _viewModel.RefreshReviewRows();
        Filter.SelectedIndex = 0;
    }

    private void FilterChanged(object? sender, EventArgs eventArgs)
    {
        Rows.ItemsSource = _viewModel.ReviewRows.Where(row => Filter.SelectedIndex switch
        {
            1 => row.State.StartsWith("Pending", StringComparison.Ordinal)
          , 2 => row.State.Contains("attention", StringComparison.Ordinal)
          , 3 => row.State is "New item" or "Existing item link"
          , 4 => row.State == "Skipped"
          , _ => true
        }).ToList();
    }

    private async void RowSelected(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (eventArgs.CurrentSelection.FirstOrDefault() is not MuseReviewRow row) return;
        _viewModel.ReviewRow(row.Index);
        await Navigation.PopModalAsync();
    }

    private async void CloseList(object? sender, EventArgs eventArgs) => await Navigation.PopModalAsync();
}
