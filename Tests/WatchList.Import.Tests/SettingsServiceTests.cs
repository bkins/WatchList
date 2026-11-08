using WatchLists.Services;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class SettingsServiceTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\" \"")]
    public async Task GetWatchedCategoryAsync_EmptyStoredValue_ReturnsDefaultWithoutOverwritingFile(string json)
    {
        using var database = new DisposableImportDatabase();
        var filePath = Path.Combine(database.DirectoryPath, "WatchedCategory.json");
        await File.WriteAllTextAsync(filePath, json);
        var settings = new SettingsService(database.DirectoryPath);

        var category = await settings.GetWatchedCategoryAsync();

        Assert.Equal("Finished Watching", category);
        Assert.Equal(json, await File.ReadAllTextAsync(filePath));
    }

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
