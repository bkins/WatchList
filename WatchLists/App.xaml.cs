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

        protected override void OnSleep()
        {
            Log.CloseAndFlush();
            base.OnSleep();
        }

    }
}
