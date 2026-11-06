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

    [Fact]
    public void Merge_TwoCurrentDevicesExchangeDistinctSources_ConvergesWithoutDuplicates()
    {
        SQLitePCL.Batteries_V2.Init();
        using var first = new SQLiteConnection(":memory:");
        using var second = new SQLiteConnection(":memory:");
        first.CreateTable<WatchItem>();
        second.CreateTable<WatchItem>();
        var id = Guid.NewGuid();
        var item = new WatchItem
                   {
                       Id          = id
                     , Title       = "Synthetic film"
                     , MovieId     = 42
                     , ApiSource   = "TMDB"
                     , Category    = "Consider Watching"
                     , LastUpdated = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)
                   };
        first.Insert(item);
        second.Insert(item);
        var firstStore = new RecommendationStore(first);
        var secondStore = new RecommendationStore(second);
        var now = DateTime.UtcNow;
        firstStore.Apply([new(id, "https://example.com/one", "first source")], now);
        secondStore.Apply([new(id, "https://example.com/two", "second source")], now);

        new WatchItemSyncMerger(first).Merge(second.Table<WatchItem>().ToList(), secondStore.GetAll());
        new WatchItemSyncMerger(second).Merge(first.Table<WatchItem>().ToList(), firstStore.GetAll());
        new WatchItemSyncMerger(first).Merge(second.Table<WatchItem>().ToList(), secondStore.GetAll());

        Assert.Equal(2, firstStore.GetAll().Count);
        Assert.Equal(2, secondStore.GetAll().Count);
        Assert.Single(first.Table<WatchItem>().ToList());
        Assert.Single(second.Table<WatchItem>().ToList());
    }
}
