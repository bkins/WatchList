using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using WatchLists.MVVM.Models;
using WatchLists.Logger;
using WatchLists.Services.MuseImport;

namespace WatchLists.MVVM.ViewModels;

public class WatchItemGroup : ObservableCollection<WatchItem>
{
    public string CategoryName { get; private set; }
    public ICommand ToggleExpandCommand { get; set; }

    public new ObservableCollection<WatchItem> Items => this;
    public ObservableCollection<UnverifiedMuseItem> UnverifiedItems { get; } = new();

    private bool _isExpanded = true;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            var startedUtc = DateTime.UtcNow;
            FileLogger.WriteLog($"Category expander transition started: category='{CategoryName}', expanded={value}, watchItems={Items.Count}, unverifiedItems={UnverifiedItems.Count}.");
            _isExpanded = value;
            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsExpanded)));
            FileLogger.WriteLog($"Category expander transition completed: category='{CategoryName}', expanded={value}, elapsedMs={(DateTime.UtcNow - startedUtc).TotalMilliseconds:F0}.");
        }
    }

    public int ItemCount => Items.Count + UnverifiedItems.Count;

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
