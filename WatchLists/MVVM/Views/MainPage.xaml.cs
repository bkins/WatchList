using WatchLists.MVVM.ViewModels;
using WatchLists.MVVM.Models;
using CommunityToolkit.Maui.Views;

namespace WatchLists.MVVM.Views
{
    public partial class MainPage : ContentPage
    {
        public WatchListViewModel ViewModel { get; }

        public MainPage()
        {
            ViewModel = App.Current.Services.GetRequiredService<WatchListViewModel>();
            InitializeComponent();
            VersionLabel.Text = $"Version {WatchListVersion.DisplayVersion}";
            BindingContext = ViewModel;
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (HeaderScroll != null)
            {
                HeaderScroll.MaximumHeightRequest = WatchListLayout.GetHeaderMaximumHeight(height);
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            if (BindingContext is WatchListViewModel vm && vm.IsSelectionMode)
            {
                vm.ToggleSelectionMode();
            }
        }

        private void ConfigureCategoryExpander(object? sender, EventArgs eventArgs)
        {
            if (sender is not Expander expander)
            {
                return;
            }

            expander.HandleHeaderTapped = tappedEventArgs =>
            {
                if (expander.BindingContext is WatchItemGroup categoryGroup)
                {
                    _ = categoryGroup.ToggleExpandedAfterRenderAsync();
                }
            };
        }

        private async void OpenUnverifiedSource(object? sender, TappedEventArgs eventArgs)
        {
            if (sender is not BindableObject { BindingContext: UnverifiedMuseItem item } ||
                !Uri.TryCreate(item.SourceUrl, UriKind.Absolute, out var sourceUri)) return;
            try
            {
                await Launcher.OpenAsync(sourceUri);
            }
            catch (Exception exception)
            {
                await WatchLists.Logger.FileLogger.WriteLogAsync($"Could not open unverified Muse source '{item.SourceUrl}': {exception.Message}");
            }
        }

        private async void ReimportUnverified(object? sender, EventArgs eventArgs)
        {
            if (sender is not BindableObject { BindingContext: UnverifiedMuseItem item }) return;
            var viewModel = App.Current.Services.GetRequiredService<MuseImportPreviewViewModel>();
            if (viewModel.HasReview &&
                !await DisplayAlertAsync("Replace current review?", "Starting this focused review will discard the current unsaved Muse review and its decisions.", "Replace", "Cancel")) return;
            await viewModel.BeginUnverifiedReviewAsync(item);
            await Shell.Current.GoToAsync(nameof(MuseImportPreviewPage));
        }
    }
}
