using Serilog;

namespace WatchLists
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }

        public App (IServiceProvider services)
        {
            InitializeComponent();
            Services = services;
            MainPage = new AppShell();

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
