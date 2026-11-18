namespace WatchLists;

public sealed class BusyOperationTracker
{
    private static readonly Lazy<BusyOperationTracker> Tracker = new(() => new BusyOperationTracker());
    private readonly object _sync = new();
    private int _activeOperationCount;
    private bool _isBusy;

    public static BusyOperationTracker Current => Tracker.Value;

    public bool IsBusy
    {
        get
        {
            lock (_sync)
            {
                return _isBusy;
            }
        }
    }

    public event EventHandler<bool>? BusyChanged;

    public async Task WaitForOverlayAsync()
    {
        await Task.Delay(TimeSpan.FromMilliseconds(50));
    }

    public IDisposable BeginOperation()
    {
        var notifyBusy = false;
        lock (_sync)
        {
            _activeOperationCount++;
            if (!_isBusy)
            {
                _isBusy = true;
                notifyBusy = true;
            }
        }

        if (notifyBusy)
        {
            BusyChanged?.Invoke(this, true);
        }

        return new BusyOperation(this);
    }

    public async Task RunAsync(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using (BeginOperation())
        {
            await WaitForOverlayAsync();
            await operation();
        }
    }

    public async Task<TResult> RunAsync<TResult>(Func<Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using (BeginOperation())
        {
            await WaitForOverlayAsync();
            return await operation();
        }
    }

    internal void ReleaseOperation()
    {
        var notifyIdle = false;
        lock (_sync)
        {
            if (_activeOperationCount == 0)
            {
                return;
            }

            _activeOperationCount--;
            if (_activeOperationCount == 0 && _isBusy)
            {
                _isBusy = false;
                notifyIdle = true;
            }
        }

        if (notifyIdle)
        {
            BusyChanged?.Invoke(this, false);
        }
    }
}
