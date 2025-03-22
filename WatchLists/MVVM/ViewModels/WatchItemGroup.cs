using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchLists.MVVM.Models;

namespace WatchLists.MVVM.ViewModels;

public class WatchItemGroup : ObservableObject
{
    public string                          CategoryName        { get; private set; }
    public ObservableCollection<WatchItem> Items               { get; private set; }
    public ICommand                        ToggleExpandCommand { get; set; }


    private bool _isExpanded = true;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded
                         , value);
    }

    public WatchItemGroup(string categoryName)
    {
        CategoryName        = categoryName;
        Items               = new ObservableCollection<WatchItem>();
        ToggleExpandCommand = new RelayCommand<WatchItemGroup>(ExecuteToggleExpandCommand);
    }

    private void ExecuteToggleExpandCommand(WatchItemGroup? group)
    {
        if (group != null)
        {
            IsExpanded = ! IsExpanded;
        }
    }
}
