using System.Text.Json;
using SQLite;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class RecommendationSyncTests
{
    [Fact]
    public void MergeFromSync_DifferentLocalIdAndRepeatedSource_MergesDescriptionsAndDates()
    {
        SQLitePCL.Batteries_V2.Init();
        using var connection = new SQLiteConnection(":memory:");
        connection.CreateTable<TestWatchItem>();
        var localId = Guid.NewGuid();
        var remoteId = Guid.NewGuid();
        connection.Insert(new TestWatchItem { Id = localId });
        var store = new RecommendationStore(connection);
        var first = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        store.Apply([new(localId, "https://instagram.com/reel/abc", "local")], first.AddDays(1));
        var remote = new MovieRecommendation
                     {
                         WatchItemId      = remoteId
                       , SourceUrl        = "https://www.instagram.com/reel/abc/?utm=tracking"
                       , DescriptionsJson = JsonSerializer.Serialize(new[] { "remote", "local" })
                       , FirstImportedUtc = first
                       , LastSeenUtc      = first.AddDays(2)
                     };

        var added = store.MergeFromSync([remote], new Dictionary<Guid, Guid> { [remoteId] = localId });

        var result = Assert.Single(store.GetAll());
        Assert.Equal(0, added);
        Assert.Equal(first, result.FirstImportedUtc);
        Assert.Equal(first.AddDays(2), result.LastSeenUtc);
        Assert.Equal(new[] { "local", "remote" }, JsonSerializer.Deserialize<string[]>(result.DescriptionsJson));
    }

    [Fact]
    public void MergeFromSync_MissingMapping_RollsBackEarlierRows()
    {
        SQLitePCL.Batteries_V2.Init();
        using var connection = new SQLiteConnection(":memory:");
        connection.CreateTable<TestWatchItem>();
        var localId = Guid.NewGuid();
        var remoteId = Guid.NewGuid();
        connection.Insert(new TestWatchItem { Id = localId });
        var store = new RecommendationStore(connection);
        var now = DateTime.UtcNow;
        var valid = new MovieRecommendation
                    {
                        WatchItemId      = remoteId
                      , SourceUrl        = "https://example.com/one"
                      , DescriptionsJson = "[\"description\"]"
                      , FirstImportedUtc = now
                      , LastSeenUtc      = now
                    };
        var missing = new MovieRecommendation
                      {
                          WatchItemId      = Guid.NewGuid()
                        , SourceUrl        = "https://example.com/two"
                        , DescriptionsJson = "[\"description\"]"
                        , FirstImportedUtc = now
                        , LastSeenUtc      = now
                      };

        Assert.Throws<InvalidOperationException>(() => store.MergeFromSync([valid, missing]
                                                                        , new Dictionary<Guid, Guid> { [remoteId] = localId }));

        Assert.Empty(store.GetAll());
    }
}
