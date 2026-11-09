using System.Text.Json;
using Moq;
using WatchLists.MVVM.Models;
using WatchLists.MVVM.ViewModels;
using WatchLists.Services;
using WatchLists.Services.Interfaces;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class EditWatchItemViewModelTests
{
    [Fact]
    public async Task Save_ReopenedWatchedItem_RestoresCategoryAndPreservesItemAndRecommendations()
    {
        using var database = new DisposableImportDatabase();
        var settings = new SettingsService(database.DirectoryPath);
        await settings.SaveWatchedCategoryAsync("Watched Archive");
        var item = new WatchItem
                   {
                       Title                      = "Synthetic film"
                     , Category                   = "Weekend shortlist"
                     , StreamingService           = "Netflix"
                     , DeepLinkUri                = "https://example.test/play"
                     , IsLiked                    = true
                     , Type                       = "Movie"
                     , MovieId                    = 123
                     , ApiSource                  = "TMDB"
                     , Overview                   = "Synthetic overview"
                     , PosterUrl                  = "https://example.test/poster"
                     , AvailableStreamingServices = "Netflix, Hulu"
                     , AggregatedDataJson         = """{"Synthetic":"metadata"}"""
                   };
        database.Connection.Insert(item);
        var recommendations = new RecommendationStore(database.Connection);
        recommendations.Apply([new(item.Id, "https://example.test/first", "First recommendation")
                             , new(item.Id, "https://example.test/second", "Second recommendation")]
                             , DateTime.UtcNow);
        var originalRecommendations = JsonSerializer.Serialize(recommendations.GetAll().OrderBy(link => link.SourceKey));

        using (var service = new WatchListService(settings, database.DatabasePath))
        {
            var editor = CreateEditor(service, settings);
            await editor.LoadItem(item.Id.ToString());
            var categoryChanges = new List<string>();
            editor.EditableItem.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(WatchItem.Category))
                    categoryChanges.Add(editor.EditableItem.Category);
            };

            editor.ToggleWatchedCommand.Execute(true);
            await editor.Save();

            Assert.Equal(["Watched Archive"], categoryChanges);
            Assert.Contains("Watched Archive", editor.Categories);
        }

        var watched = database.Connection.Get<WatchItem>(item.Id);
        Assert.True(watched.IsWatched);
        Assert.Equal("Watched Archive", watched.Category);
        Assert.Equal(item.Category, watched.PreviousCategory);

        using (var service = new WatchListService(settings, database.DatabasePath))
        {
            var editor = CreateEditor(service, settings);
            await editor.LoadItem(item.Id.ToString());
            editor.ToggleWatchedCommand.Execute(false);
            await editor.Save();

            Assert.Contains(item.Category, editor.Categories);
        }

        var restored = Assert.Single(database.Connection.Table<WatchItem>());
        Assert.False(restored.IsWatched);
        Assert.Empty(restored.PreviousCategory);
        Assert.Equal(item.Id, restored.Id);
        Assert.Equal(item.Category, restored.Category);
        Assert.Equal(item.Title, restored.Title);
        Assert.Equal(item.StreamingService, restored.StreamingService);
        Assert.Equal(item.DeepLinkUri, restored.DeepLinkUri);
        Assert.Equal(item.IsLiked, restored.IsLiked);
        Assert.Equal(item.IsDeleted, restored.IsDeleted);
        Assert.Equal(item.Type, restored.Type);
        Assert.Equal(item.MovieId, restored.MovieId);
        Assert.Equal(item.ApiSource, restored.ApiSource);
        Assert.Equal(item.Overview, restored.Overview);
        Assert.Equal(item.PosterUrl, restored.PosterUrl);
        Assert.Equal(item.AvailableStreamingServices, restored.AvailableStreamingServices);
        Assert.Equal(item.AggregatedDataJson, restored.AggregatedDataJson);
        Assert.Equal(originalRecommendations, JsonSerializer.Serialize(recommendations.GetAll().OrderBy(link => link.SourceKey)));
    }

    [Theory]
    [InlineData("Consider Watching", "Consider Watching")]
    [InlineData("", "Finished Watching")]
    [InlineData("  ", "Finished Watching")]
    public async Task Save_ExistingWatchedItem_RestoresOnlyRecordedCategory(string previousCategory, string expectedCategory)
    {
        using var database = new DisposableImportDatabase();
        var settings = new SettingsService(database.DirectoryPath);
        using var service = new WatchListService(settings, database.DatabasePath);
        var item = new WatchItem
                   {
                       Title            = "Previously watched"
                     , Category         = "Finished Watching"
                     , PreviousCategory = previousCategory
                     , IsWatched        = true
                   };
        database.Connection.Insert(item);
        var editor = CreateEditor(service, settings);
        await editor.LoadItem(item.Id.ToString());

        editor.ToggleWatchedCommand.Execute(false);
        await editor.Save();

        var saved = database.Connection.Get<WatchItem>(item.Id);
        Assert.False(saved.IsWatched);
        Assert.Equal(expectedCategory, saved.Category);
        Assert.Empty(saved.PreviousCategory);
    }

    [Fact]
    public async Task Save_WatchedStateReappliedWhileLoading_KeepsManuallyChosenCategoryAndHistory()
    {
        using var database = new DisposableImportDatabase();
        var settings = new SettingsService(database.DirectoryPath);
        using var service = new WatchListService(settings, database.DatabasePath);
        var item = new WatchItem
                   {
                       Title            = "Previously watched"
                     , Category         = "Currently Watching"
                     , PreviousCategory = "Consider Watching"
                     , IsWatched        = true
                   };
        database.Connection.Insert(item);
        var editor = CreateEditor(service, settings);
        await editor.LoadItem(item.Id.ToString());

        editor.ToggleWatchedCommand.Execute(true);
        await editor.Save();

        var saved = database.Connection.Get<WatchItem>(item.Id);
        Assert.True(saved.IsWatched);
        Assert.Equal(item.Category, saved.Category);
        Assert.Equal(item.PreviousCategory, saved.PreviousCategory);
    }

    private static EditWatchItemViewModel CreateEditor(WatchListService service, SettingsService settings)
    {
        return new EditWatchItemViewModel(service, settings, Mock.Of<IMovieDataAggregator>(MockBehavior.Strict));
    }
}
