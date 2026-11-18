using Xunit;

namespace WatchList.Import.Tests;

public class BusyOperationTrackerTests
{
    [Fact]
    public void BeginOperation_WithOverlappingOperations_StaysBusyUntilLastOperationEnds()
    {
        var tracker = new WatchLists.BusyOperationTracker();
        var busyChanges = new List<bool>();
        tracker.BusyChanged += (sender, isBusy) => busyChanges.Add(isBusy);

        var firstOperation = tracker.BeginOperation();
        var secondOperation = tracker.BeginOperation();

        Assert.True(tracker.IsBusy);
        firstOperation.Dispose();
        Assert.True(tracker.IsBusy);

        secondOperation.Dispose();

        Assert.False(tracker.IsBusy);
        Assert.Equal(new[] { true, false }, busyChanges);
    }

    [Fact]
    public async Task RunAsync_WhenOperationThrows_ReleasesBusyState()
    {
        var tracker = new WatchLists.BusyOperationTracker();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => tracker.RunAsync(() =>
            Task.FromException(new InvalidOperationException("Expected failure."))));

        Assert.Equal("Expected failure.", exception.Message);
        Assert.False(tracker.IsBusy);
    }
}
