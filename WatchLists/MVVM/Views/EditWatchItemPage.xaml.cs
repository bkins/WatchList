using WatchLists.MVVM.ViewModels;

namespace WatchLists.MVVM.Views;

public partial class EditWatchItemPage : ContentPage, IQueryAttributable
{
    public EditWatchItemPage()
    {
        InitializeComponent();
        BindingContext = App.Current.Services.GetService<EditWatchItemViewModel>();
    }

    public void ApplyQueryAttributes(IDictionary<string, object?> query)
    {
        if (BindingContext is not EditWatchItemViewModel viewModel) return;

        viewModel.LoadItem(query.TryGetValue("watchItemId"
                                           , out object? watchItemId)
                               ? watchItemId?.ToString()
                               : null); // No ID means we're adding a new item
    }

    private async void OnStreamingServiceSelectionChanged (object    sender
                                                         , EventArgs eventArgs)
    {
        if (sender is not Picker { SelectedItem: string selectedStreamingService }) return;

        var editWatchItemVm = BindingContext as EditWatchItemViewModel;
        await (editWatchItemVm?.OnStreamingServiceSelectedAsync(selectedStreamingService)).ConfigureAwait(false);
    }

    private async void OnCategorySelectionChanged(object    sender
                                                , EventArgs eventArgs)
    {
        if (sender is not Picker { SelectedItem: string selectedCategory }) return;

        var editWatchItemVm = BindingContext as EditWatchItemViewModel;
        await (editWatchItemVm?.OnCategorySelectedAsync(selectedCategory)).ConfigureAwait(false);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is not EditWatchItemViewModel viewModel) return;

        await viewModel.InitializeAsync();
    }
}
