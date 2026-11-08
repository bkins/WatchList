using CommunityToolkit.Mvvm.ComponentModel;

namespace WatchLists.MVVM.ViewModels;

public partial class CategoryStatCardItem : ObservableObject
{
    [ObservableProperty] public partial string CategoryName { get; set; } = string.Empty;
    [ObservableProperty] public partial int Count { get; set; }
    [ObservableProperty] public partial bool IsSelected { get; set; }
    public System.Windows.Input.ICommand? SelectCommand { get; set; }
}
