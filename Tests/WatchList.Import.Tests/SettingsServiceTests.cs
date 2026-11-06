using WatchLists.Services;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task SaveAutoSyncEnabledAsync_Disabled_PersistsAcrossServiceInstances()
    {
        var folder = Path.Combine(Path.GetTempPath(), "WatchListSettingsTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var settings = new SettingsService(folder);
            await settings.SaveSyncModeAsync("FileSystem");

            await settings.SaveAutoSyncEnabledAsync(false);
            var reloaded = new SettingsService(folder);

            Assert.False(reloaded.GetAutoSyncEnabled());
            Assert.Equal("FileSystem", reloaded.GetSyncMode());
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
