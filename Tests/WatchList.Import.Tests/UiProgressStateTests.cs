using Xunit;

namespace WatchList.Import.Tests;

public class UiProgressStateTests
{
    [Fact]
    public async Task HideAfterAsync_WhenNewProgressStarts_KeepsNewProgressVisible()
    {
        var progressState = WatchLists.UiProgressState.Current;
        var firstSequence = progressState.Show("First activity");
        var firstHideTask = progressState.HideAfterAsync(firstSequence, 40);
        var secondSequence = progressState.Show("Second activity");

        await firstHideTask;

        Assert.True(progressState.IsVisible);
        Assert.Equal("Second activity", progressState.StatusText);

        await progressState.HideAfterAsync(secondSequence, 1);

        Assert.False(progressState.IsVisible);
    }
}
