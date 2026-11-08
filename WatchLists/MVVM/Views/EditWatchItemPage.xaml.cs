using WatchLists.MVVM.ViewModels;

namespace WatchLists.MVVM.Views;

public partial class EditWatchItemPage : ContentPage, IQueryAttributable
{
    public EditWatchItemPage()
    {
        InitializeComponent();
        BindingContext = App.Current.Services.GetService<EditWatchItemViewModel>();
        _ = WatchLists.Logger.FileLogger.WriteLogAsync($"[EditWatchItemPage] Constructor. viewModel: {BindingContext?.GetHashCode()}");
    }

    public async void ApplyQueryAttributes(IDictionary<string, object?> query)
    {
        if (BindingContext is not EditWatchItemViewModel viewModel) return;

        await viewModel.LoadItem(query.TryGetValue("watchItemId"
                                           , out object? watchItemId)
                               ? watchItemId?.ToString()
                               : null); // No ID means we're adding a new item

        if (query.TryGetValue("PreFilledTitle", out object? preFilledTitleObj) && preFilledTitleObj is string preFilledTitle && preFilledTitle.HasValue())
        {
            viewModel.MovieTitle = preFilledTitle;
            if (viewModel.EditableItem != null)
            {
                viewModel.EditableItem.Title = preFilledTitle;
            }
        }
    }

    private async void OnStreamingServiceSelectionChanged (object    sender
                                                         , EventArgs eventArgs)
    {
        if (sender is not Picker { SelectedItem: string selectedStreamingService }) return;

        if (BindingContext is EditWatchItemViewModel viewModel)
            await viewModel.OnStreamingServiceSelectedAsync(selectedStreamingService);
    }

    private async void OnCategorySelectionChanged(object    sender
                                                , EventArgs eventArgs)
    {
        if (sender is not Picker { SelectedItem: string selectedCategory }) return;

        if (BindingContext is EditWatchItemViewModel viewModel)
            await viewModel.OnCategorySelectedAsync(selectedCategory);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is not EditWatchItemViewModel viewModel) return;

        await viewModel.InitializeAsync();
    }

    private void Switch_Toggled (object?          sender
                               , ToggledEventArgs e)
    {
        var vm = BindingContext as EditWatchItemViewModel;
        vm?.ToggleWatchedCommand.Execute(e.Value);
    }
}
