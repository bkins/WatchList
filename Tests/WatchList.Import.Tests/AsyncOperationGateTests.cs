using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class AsyncOperationGateTests
{
    [Fact]
    public async Task RunAsync_OverlappingOperations_SerializesTheirWork()
    {
        var gate = new AsyncOperationGate();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        var first = gate.RunAsync(async () =>
        {
            order.Add("first started");
            started.SetResult();
            await release.Task;
            order.Add("first finished");
            return true;
        });
        await started.Task;

        var second = gate.RunAsync(() =>
        {
            order.Add("second started");
            return Task.FromResult(true);
        });
        Assert.False(second.IsCompleted);
        release.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(new[] { "first started", "first finished", "second started" }, order);
    }

    [Fact]
    public async Task RunAsync_FailingOperation_ReleasesGate()
    {
        var gate = new AsyncOperationGate();

        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.RunAsync<int>(() => throw new InvalidOperationException()));
        var result = await gate.RunAsync(() => Task.FromResult(42));

        Assert.Equal(42, result);
    }
}
