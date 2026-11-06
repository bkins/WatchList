using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class SyncSafetyGateTests
{
    [Fact]
    public async Task RunAsync_WhileSyncIsPaused_DoesNotInvokeOperation()
    {
        var invoked = false;

        var result = await SyncSafetyGate.RunAsync(() =>
        {
            invoked = true;
            return Task.FromResult(true);
        }, false);

        Assert.False(result);
        Assert.False(invoked);
    }
}
