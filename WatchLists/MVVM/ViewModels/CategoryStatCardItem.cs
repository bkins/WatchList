using CommunityToolkit.Mvvm.ComponentModel;

namespace WatchLists.MVVM.ViewModels;

public partial class CategoryStatCardItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    public partial string CategoryName { get; set; } = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    public partial int Count { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    public partial bool IsSelected { get; set; }
    public string DisplayText => $"{CategoryName} · {Count}" + (IsSelected ? " · selected" : string.Empty);

    public System.Windows.Input.ICommand? SelectCommand { get; set; }
}
