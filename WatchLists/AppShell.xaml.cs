using WatchLists.MVVM.Views;

namespace WatchLists
{
    public partial class AppShell : Shell
    {
        private bool _isNavigationPending;
        private string _pageTitle = "WatchList";

        public bool IsNavigationPending
        {
            get => _isNavigationPending;
            private set
            {
                if (_isNavigationPending == value) return;
                _isNavigationPending = value;
                OnPropertyChanged(nameof(IsNavigationPending));
            }
        }

        public string PageTitle
        {
            get => _pageTitle;
            private set
            {
                if (_pageTitle == value) return;
                _pageTitle = value;
                OnPropertyChanged(nameof(PageTitle));
            }
        }

        public AppShell()
        {
            InitializeComponent();
            Title = $"WatchList · v{WatchListVersion.DisplayVersion}";
            PageTitle = Title;
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

        private void HandleNavigating(object? sender, ShellNavigatingEventArgs eventArgs)
        {
            IsNavigationPending = true;
        }

        private void HandleNavigated(object? sender, ShellNavigatedEventArgs eventArgs)
        {
            IsNavigationPending = false;
            if (CurrentPage is MainPage)
            {
                PageTitle = Title;
                return;
            }

            var currentPageTitle = CurrentPage?.Title;
            PageTitle = string.IsNullOrWhiteSpace(currentPageTitle)
                ? Title
                : currentPageTitle;
        }
    }
}
