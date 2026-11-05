using SQLite;
using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class WatchItemSyncMergerTests
{
    [Fact]
    public void Merge_RecommendationWithSameTitleAndDifferentProvider_CreatesSeparateItem()
    {
        SQLitePCL.Batteries_V2.Init();
        using var connection = new SQLiteConnection(":memory:");
        connection.CreateTable<WatchItem>();
        var existing = new WatchItem
                       {
                           Title     = "Alone"
                         , MovieId   = 1
                         , ApiSource = "TMDB"
                         , IsWatched = true
                       };
        connection.Insert(existing);
        var incoming = new WatchItem
                       {
                           Title     = "Alone"
                         , MovieId   = 2
                         , ApiSource = "TMDB"
                       };
        var now = DateTime.UtcNow;
        var recommendation = new MovieRecommendation
                             {
                                 WatchItemId      = incoming.Id
                               , SourceUrl        = "https://example.com/one"
                               , DescriptionsJson = "[\"description\"]"
                               , FirstImportedUtc = now
                               , LastSeenUtc      = now
                             };

        var result = new WatchItemSyncMerger(connection).Merge([incoming], [recommendation]);

        Assert.Equal(1, result.InsertedItems);
        Assert.Equal(1, result.InsertedRecommendations);
        Assert.True(connection.Find<WatchItem>(existing.Id).IsWatched);
        Assert.Equal(incoming.Id, Assert.Single(new RecommendationStore(connection).GetAll()).WatchItemId);
    }

    [Fact]
    public void Merge_InvalidRecommendation_RollsBackItemInsertion()
    {
        SQLitePCL.Batteries_V2.Init();
        using var connection = new SQLiteConnection(":memory:");
        connection.CreateTable<WatchItem>();
        var incoming = new WatchItem { Title = "Film" };
        var recommendation = new MovieRecommendation
                             {
                                 WatchItemId      = incoming.Id
                               , SourceUrl        = "not a URL"
                               , DescriptionsJson = "[\"description\"]"
                               , FirstImportedUtc = DateTime.UtcNow
                               , LastSeenUtc      = DateTime.UtcNow
                             };

        Assert.Throws<ArgumentException>(() => new WatchItemSyncMerger(connection).Merge([incoming], [recommendation]));

        Assert.Empty(connection.Table<WatchItem>().ToList());
    }

    [Fact]
    public void Merge_DeletedLocalProviderMatch_KeepsDeletedAndMapsRecommendation()
    {
        SQLitePCL.Batteries_V2.Init();
        using var connection = new SQLiteConnection(":memory:");
        connection.CreateTable<WatchItem>();
        var existing = new WatchItem
                       {
                           Title       = "Film"
                         , MovieId     = 42
                         , ApiSource   = "TMDB"
                         , IsDeleted   = true
                         , LastUpdated = DateTime.UtcNow.AddDays(-1)
                       };
        connection.Insert(existing);
        var incoming = new WatchItem
                       {
                           Title       = "Film"
                         , MovieId     = 42
                         , ApiSource   = "TMDB"
                         , LastUpdated = DateTime.UtcNow
                       };
        var now = DateTime.UtcNow;
        var recommendation = new MovieRecommendation
                             {
                                 WatchItemId      = incoming.Id
                               , SourceUrl        = "https://example.com/one"
                               , DescriptionsJson = "[\"description\"]"
                               , FirstImportedUtc = now
                               , LastSeenUtc      = now
                             };

        new WatchItemSyncMerger(connection).Merge([incoming], [recommendation]);

        Assert.True(connection.Find<WatchItem>(existing.Id).IsDeleted);
        Assert.Equal(existing.Id, Assert.Single(new RecommendationStore(connection).GetAll()).WatchItemId);
        Assert.Single(connection.Table<WatchItem>().ToList());
    }
}
