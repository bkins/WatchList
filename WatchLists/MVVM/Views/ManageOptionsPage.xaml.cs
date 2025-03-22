using WatchLists.MVVM.ViewModels;

namespace WatchLists.MVVM.Views;

public partial class ManageOptionsPage : ContentPage
{
    public ManageOptionsPage()
    {
        InitializeComponent();
        BindingContext = App.Current.Services.GetService<ManageOptionsViewModel>();
    }
}
