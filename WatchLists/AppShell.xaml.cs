using WatchLists.MVVM.Views;

namespace WatchLists
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Title = $"WatchList · v{AppInfo.Current.VersionString}";

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
    }
}
