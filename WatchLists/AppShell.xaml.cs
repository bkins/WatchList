using WatchLists.MVVM.Views;

namespace WatchLists
{
    public partial class AppShell : Shell
    {
        private IDisposable? _navigationOperation;

        public AppShell()
        {
            InitializeComponent();
            Title = $"WatchList · v{WatchListVersion.DisplayVersion}";
            Navigating += HandleNavigating;
            Navigated += HandleNavigated;

            Routing.RegisterRoute(nameof(EditWatchItemPage)
                                , typeof(EditWatchItemPage));

            Routing.RegisterRoute(nameof(SettingsPage)
                                , typeof(SettingsPage));

            Routing.RegisterRoute(nameof(ManageOptionsPage)
                                , typeof(ManageOptionsPage));

            Routing.RegisterRoute(nameof(SearchPage)
                                , typeof(SearchPage));

            Routing.RegisterRoute(nameof(MuseImportPreviewPage)
                                , typeof(MuseImportPreviewPage));

            Routing.RegisterRoute(nameof(MovieDetailsPage)
                                , typeof(MovieDetailsPage));

            Routing.RegisterRoute(nameof(ApiTestPage)
                                , typeof(ApiTestPage));

            Routing.RegisterRoute(nameof(LogsPage)
                                , typeof(LogsPage));
        }

        private async void HandleNavigating(object? sender, ShellNavigatingEventArgs eventArgs)
        {
            _navigationOperation?.Dispose();
            _navigationOperation = BusyOperationTracker.Current.BeginOperation();
            var navigationDeferral = eventArgs.GetDeferral();
            if (navigationDeferral is null)
            {
                _navigationOperation.Dispose();
                _navigationOperation = null;
                return;
            }

            await BusyOperationTracker.Current.WaitForOverlayAsync();
            navigationDeferral.Complete();
        }

        private void HandleNavigated(object? sender, ShellNavigatedEventArgs eventArgs)
        {
            _navigationOperation?.Dispose();
            _navigationOperation = null;
        }
    }
}
