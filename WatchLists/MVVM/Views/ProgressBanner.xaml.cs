namespace WatchLists.MVVM.Views;

public partial class ProgressBanner : ContentView
{
    public ProgressBanner()
    {
        InitializeComponent();
        BindingContext = UiProgressState.Current;
    }
}
