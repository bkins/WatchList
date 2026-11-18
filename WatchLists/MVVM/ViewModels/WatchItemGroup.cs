using System.Collections.ObjectModel;
using WatchLists.MVVM.Models;
using WatchLists.Logger;
using WatchLists.Services.MuseImport;

namespace WatchLists.MVVM.ViewModels;

public class WatchItemGroup : ObservableCollection<WatchItem>
{
    private static readonly SemaphoreSlim TransitionLogLock = new(1, 1);
    private bool _isTransitioning;
    private long _transitionSequence;

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
            var transition = BeginTransition(value, startedUtc);
            SetExpandedState(value);
            _ = ClearTransitionIndicatorAsync(transition.Sequence, transition.ProgressSequence, value, startedUtc);
        }
    }

    public bool IsTransitioning
    {
        get => _isTransitioning;
        private set
        {
            if (_isTransitioning == value) return;
            _isTransitioning = value;
            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsTransitioning)));
        }
    }

    public int ItemCount => Items.Count + UnverifiedItems.Count;

    public async Task ToggleExpandedAfterRenderAsync()
    {
        if (IsTransitioning)
        {
            return;
        }

        var expanded = !IsExpanded;
        var startedUtc = DateTime.UtcNow;
        var transition = BeginTransition(expanded, startedUtc);

        await Task.Delay(TimeSpan.FromMilliseconds(120));

        if (Interlocked.Read(ref _transitionSequence) != transition.Sequence)
        {
            return;
        }

        SetExpandedState(expanded);
        await ClearTransitionIndicatorAsync(transition.Sequence, transition.ProgressSequence, expanded, startedUtc);
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

    private (long Sequence, long ProgressSequence) BeginTransition(bool expanded, DateTime startedUtc)
    {
        IsTransitioning = true;
        var transitionSequence = Interlocked.Increment(ref _transitionSequence);
        var progressSequence = UiProgressState.Current.Show($"Updating {CategoryName}...");
        WriteTransitionLog($"Category transition requested: category='{CategoryName}', expanded={expanded}, watchItems={Items.Count}, unverifiedItems={UnverifiedItems.Count}, startedUtc={startedUtc:O}.");
        return (transitionSequence, progressSequence);
    }

    private void SetExpandedState(bool expanded)
    {
        _isExpanded = expanded;
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsExpanded)));
    }

    private async Task ClearTransitionIndicatorAsync(
        long transitionSequence,
        long progressSequence,
        bool expanded,
        DateTime startedUtc)
    {
        await UiProgressState.Current.HideAfterAsync(progressSequence, 1200);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Interlocked.Read(ref _transitionSequence) == transitionSequence)
            {
                IsTransitioning = false;
                WriteTransitionLog($"Category transition feedback completed: category='{CategoryName}', expanded={expanded}, elapsedMs={(DateTime.UtcNow - startedUtc).TotalMilliseconds:F0}.");
            }
        });
    }

}
