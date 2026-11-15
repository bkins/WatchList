using WatchLists.Services;
using WatchLists.Services.Enums;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task GetOptionsAsync_ExistingCategories_AddsUnverifiedCategory()
    {
        using var database = new DisposableImportDatabase();
        var settings = new SettingsService(database.DirectoryPath);
        await settings.SaveOptionsAsync(SettingType.Categories, ["Currently Watching", "Finished Watching", "Consider Watching"]);

        var categories = await settings.GetOptionsAsync(SettingType.Categories);

        Assert.Contains("Unverified", categories);
        Assert.Equal("Unverified", categories[^1]);
    }

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
