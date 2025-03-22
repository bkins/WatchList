using WatchLists.MVVM.ViewModels;
using WatchLists.Services;

namespace WatchLists.MVVM.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = App.Current.Services.GetService<SettingsViewModel>()
                      ?? new SettingsViewModel(new SettingsService());
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SettingsViewModel vm)
        {
            await vm.LoadSettingsAsync();
        }
    }
}
