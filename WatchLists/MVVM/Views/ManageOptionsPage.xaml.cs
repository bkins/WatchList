using WatchLists.MVVM.ViewModels;

namespace WatchLists.MVVM.Views;

public partial class ManageOptionsPage : ContentPage, IQueryAttributable
{
    public ManageOptionsPage()
    {
        InitializeComponent();
        BindingContext = App.Current.Services.GetService<ManageOptionsViewModel>();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is IQueryAttributable queryAttributable)
        {
            queryAttributable.ApplyQueryAttributes(query);
        }
    }
}
