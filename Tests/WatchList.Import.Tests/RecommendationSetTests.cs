using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;
using WatchLists.Services.ReliableSync;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class RecommendationSetTests
{
    [Fact]
    public void Remove_PrimaryWithOthers_RequiresExplicitReplacementAndSurvivesRestart()
    {
        using var database = new DisposableImportDatabase();
        var item = new WatchItem { Title = "Coherence", IsWatched = true };
        database.Connection.Insert(item);
        var store = new RecommendationStore(database.Connection);
        var first = "https://www.instagram.com/reel/First/";
        var second = "https://www.instagram.com/reel/Second/";
        var now = DateTime.UtcNow;
        store.Apply([new(item.Id, first, "First source")], now);
        store.Apply([new(item.Id, second, "Second source")], now.AddSeconds(1));
        var set = new RecommendationSet(database.Connection);
        var initial = set.Read(item.Id);
        var primary = RecommendationSet.Primary(initial)!;
        var remaining = Assert.Single(initial, row => row.Id != primary.Id);

        Assert.Throws<InvalidOperationException>(() => set.Remove(item.Id, primary.Id, null));
        set.Remove(item.Id, primary.Id, remaining.Id);
        store.Apply([new(item.Id, first, "Repeated source")], now.AddSeconds(2));
        using var reopened = new SQLite.SQLiteConnection(database.DatabasePath);
        var after = new RecommendationSet(reopened).Read(item.Id);

        Assert.Equal(first, primary.SourceUrl);
        Assert.Equal(remaining.Id, RecommendationSet.Primary(after)?.Id);
        Assert.True(Assert.Single(after, row => row.Id == primary.Id).IsRemoved);
        Assert.True(reopened.Find<WatchItem>(item.Id).IsWatched);

        new RecommendationSet(reopened).Remove(item.Id, remaining.Id, null);

        Assert.Null(RecommendationSet.Primary(new RecommendationSet(reopened).Read(item.Id)));
    }

    [Fact]
    public void Save_ManualWithoutSource_PreservesPrimaryAndGatesSpoilerSearch()
    {
        using var database = new DisposableImportDatabase();
        var item = new WatchItem { Title = "Saved title" };
        database.Connection.Insert(item);
        _ = new RecommendationStore(database.Connection);
        var set = new RecommendationSet(database.Connection);
        var details = new Dictionary<RecommendationField, string>
                      {
                          [RecommendationField.Recommender]      = "Friend"
                        , [RecommendationField.Commentary]       = "The ending reveals the culprit"
                        , [RecommendationField.ContainsSpoilers] = "true"
                      };

        set.Save(item.Id, null, details, null, null);
        var initial = Assert.Single(set.Read(item.Id));
        set.Save(item.Id, null, new Dictionary<RecommendationField, string>(), null, null);
        var all = set.Read(item.Id);

        Assert.True(initial.IsManual);
        Assert.Equal(initial.Id, RecommendationSet.Primary(all)?.Id);
        Assert.Equal(["Friend"], RecommendationMetadata.SearchValues(initial));
        Assert.Equal(2, all.Select(row => row.SourceKey).Distinct().Count());
        Assert.All(all, row => Assert.Empty(row.SourceUrl));
        Assert.Equal(4, SyncEnvelope.Create(ReliableSyncStore.ReadLocal(database.Connection)).SchemaVersion);
    }
}
