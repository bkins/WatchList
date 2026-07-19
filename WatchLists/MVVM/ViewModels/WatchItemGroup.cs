using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using WatchLists.MVVM.Models;

namespace WatchLists.MVVM.ViewModels;

public class WatchItemGroup : ObservableCollection<WatchItem>
{
    public string CategoryName { get; private set; }
    public ICommand ToggleExpandCommand { get; set; }

    public ObservableCollection<WatchItem> Items => this;

    private bool _isExpanded = true;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;

            _isExpanded = value;
            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    public WatchItemGroup(string categoryName) : base()
    {
        CategoryName        = categoryName;
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
