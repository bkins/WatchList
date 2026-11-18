using System.Collections.ObjectModel;
using WatchLists.MVVM.Models;
using WatchLists.Logger;
using WatchLists.Services.MuseImport;

namespace WatchLists.MVVM.ViewModels;

public class WatchItemGroup : ObservableCollection<WatchItem>
{
    private static readonly SemaphoreSlim TransitionLogLock = new(1, 1);

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
            var startedUtc = DateTime.UtcNow;
            WriteTransitionLog($"Category expander transition started: category='{CategoryName}', expanded={value}, watchItems={Items.Count}, unverifiedItems={UnverifiedItems.Count}.");
            _isExpanded = value;
            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsExpanded)));
            WriteTransitionLog($"Category expander transition state updated: category='{CategoryName}', expanded={value}, elapsedMs={(DateTime.UtcNow - startedUtc).TotalMilliseconds:F0}.");
        }
    }

    public int ItemCount => Items.Count + UnverifiedItems.Count;

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
