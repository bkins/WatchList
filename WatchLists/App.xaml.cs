using Serilog;
using Microsoft.Maui.Controls;

namespace WatchLists
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }

        public App (IServiceProvider services)
        {
            InitializeComponent();
            Services                         = services;
            UserAppTheme = AppTheme.Dark;
            MainPage     = new AppShell();
        }

        // Add a static property for convenience
        public new static App Current => (App)Application.Current;

        protected override async void OnStart()
        {
            base.OnStart();
            await TriggerAutoImportAsync();
        }

        protected override async void OnResume()
        {
            base.OnResume();
            await TriggerAutoImportAsync();
        }

        protected override void OnSleep()
        {
            TriggerAutoExport();
            Log.CloseAndFlush();
            base.OnSleep();
        }

        private async Task TriggerAutoImportAsync()
        {
            try
            {
                var settings = Services.GetService<Services.SettingsService>();
                if (settings != null && settings.GetAutoSyncEnabled())
                {
                    var syncService = Services.GetService<Services.SyncService>();
                    if (syncService != null)
                    {
                        await syncService.ImportAndMergeSyncBundleAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _ = Logger.FileLogger.WriteLogAsync($"App TriggerAutoImportAsync error: {ex.Message}");
            }
        }

        private void TriggerAutoExport()
        {
            try
            {
                var settings = Services.GetService<Services.SettingsService>();
                if (settings != null && settings.GetAutoSyncEnabled())
                {
                    var syncService = Services.GetService<Services.SyncService>();
                    if (syncService != null)
                    {
                        _ = Task.Run(async () => await syncService.ExportSyncBundleAsync());
                    }
                }
            }
            catch (Exception ex)
            {
                _ = Logger.FileLogger.WriteLogAsync($"App TriggerAutoExport error: {ex.Message}");
            }
        }
    }
}
