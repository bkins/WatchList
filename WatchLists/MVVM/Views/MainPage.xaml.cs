using System.Diagnostics;
using Serilog;
using WatchLists.Logger;
using WatchLists.MVVM.Models;
using WatchLists.MVVM.ViewModels;

namespace WatchLists.MVVM.Views
{
    public partial class MainPage : ContentPage
    {
        //private WatchListViewModel WatchListVm => BindingContext as WatchListViewModel ?? throw new InvalidOperationException();

        public WatchListViewModel ViewModel { get; }

        public MainPage()
        {
            ViewModel = App.Current.Services.GetRequiredService<WatchListViewModel>();
            InitializeComponent();
            BindingContext = ViewModel;
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
