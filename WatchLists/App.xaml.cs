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
            Application.Current.UserAppTheme = AppTheme.Light;
            MainPage                         = new AppShell();

            //Application.Current.UserAppTheme = AppTheme.Dark; // Forces Dark Mode
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
