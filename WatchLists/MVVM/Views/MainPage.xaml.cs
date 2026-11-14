using WatchLists.MVVM.ViewModels;

namespace WatchLists.MVVM.Views
{
    public partial class MainPage : ContentPage
    {
        public WatchListViewModel ViewModel { get; }

        public MainPage()
        {
            ViewModel = App.Current.Services.GetRequiredService<WatchListViewModel>();
            InitializeComponent();
            VersionLabel.Text = $"Version {AppInfo.Current.VersionString}";
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

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is WatchListViewModel vm)
            {
                vm.RefreshItemsCommand.Execute(null);
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
    }
}
