using System.Diagnostics;
using Serilog;
using WatchLists.Logger;
using WatchLists.MVVM.Models;
using WatchLists.MVVM.ViewModels;

namespace WatchLists.MVVM.Views
{
    public partial class MainPage : ContentPage
    {
        private WatchListViewModel WatchListVm => BindingContext as WatchListViewModel ?? throw new InvalidOperationException();

        public MainPage()
        {
            InitializeComponent();
            BindingContext = App.Current.Services.GetRequiredService<WatchListViewModel>();

            _ = FileLogger.WriteLogAsync("🔥 Test log message at {DateTime.Now}");
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is WatchListViewModel vm)
            {
                vm.RefreshItemsCommand.Execute(null);
            }
        }
    }

}
