using System.Collections.ObjectModel;
using WatchLists.MVVM.Models;
using WatchLists.Logger;
using WatchLists.Services.MuseImport;

namespace WatchLists.MVVM.ViewModels;

public class WatchItemGroup : ObservableCollection<WatchItem>
{
    private static readonly SemaphoreSlim TransitionLogLock = new(1, 1);
    private bool _isTransitioning;

    public string CategoryName { get; private set; }

    public new ObservableCollection<WatchItem> Items => this;
    public ObservableCollection<UnverifiedMuseItem> UnverifiedItems { get; } = new();

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

    public int ItemCount => Items.Count + UnverifiedItems.Count;

    public async Task ToggleExpandedAsync()
    {
        if (_isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        using var busyOperation = BusyOperationTracker.Current.BeginOperation();
        var startedUtc = DateTime.UtcNow;
        var expanded = !IsExpanded;
        WriteTransitionLog($"Category transition requested: category='{CategoryName}', expanded={expanded}, watchItems={Items.Count}, unverifiedItems={UnverifiedItems.Count}, startedUtc={startedUtc:O}.");

        try
        {
            await BusyOperationTracker.Current.WaitForOverlayAsync();
            IsExpanded = expanded;
        }
        finally
        {
            _isTransitioning = false;
            WriteTransitionLog($"Category transition completed: category='{CategoryName}', expanded={expanded}, elapsedMs={(DateTime.UtcNow - startedUtc).TotalMilliseconds:F0}.");
        }
    }

    public WatchItemGroup(string categoryName) : base()
    {
        CategoryName = categoryName;
    }

    private static void WriteTransitionLog(string message)
    {
        _ = Task.Run(async () =>
        {
            await TransitionLogLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await FileLogger.WriteLogAsync(message).ConfigureAwait(false);
            }
            finally
            {
                TransitionLogLock.Release();
            }
        });
    }

}
