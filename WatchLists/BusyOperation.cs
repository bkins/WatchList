namespace WatchLists;

internal sealed class BusyOperation : IDisposable
{
    private BusyOperationTracker? _tracker;

    public BusyOperation(BusyOperationTracker tracker)
    {
        _tracker = tracker;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _tracker, null)?.ReleaseOperation();
    }
}
