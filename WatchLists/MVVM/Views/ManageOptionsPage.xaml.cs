using WatchLists.MVVM.ViewModels;

namespace WatchLists.MVVM.Views;

public partial class ManageOptionsPage : ContentPage, IQueryAttributable
{
    public ManageOptionsViewModel ViewModel { get; }

    public ManageOptionsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<ManageOptionsViewModel>();
        InitializeComponent();
        BindingContext = ViewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is IQueryAttributable queryAttributable)
        {
            queryAttributable.ApplyQueryAttributes(query);
        }
    }
}
