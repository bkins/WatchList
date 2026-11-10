using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;
using WatchLists.Services.ReliableSync;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class SyncSnapshotMergerTests
{
    [Fact]
    public void Merge_ConcurrentRecommendationChanges_ReviewsTheSetAndPreservesIndependentItemEdits()
    {
        using var database = new DisposableImportDatabase();
        var item = new WatchItem { Title = "Saved title", Category = "Queue" };
        database.Connection.Insert(item);
        var store = new RecommendationStore(database.Connection);
        var now = DateTime.UtcNow;
        store.Apply([new(item.Id, "https://example.test/first", "First"),
                     new(item.Id, "https://example.test/second", "Second")], now);
        var baseline = ReliableSyncStore.ReadLocal(database.Connection);
        var first = baseline.Links[0];
        var second = baseline.Links[1];
        var local = new SyncSnapshot([baseline.Items[0] with { IsLiked = true }]
                                   , [first with { IsPrimary = null, IsRemoved = true }, second with { IsPrimary = true }]);
        var remote = new SyncSnapshot(baseline.Items
                                    , [first with { EditedMetadata = """{"Commentary":"A cloud correction"}""" }, second]);
        var merger = new SyncSnapshotMerger();

        var conflict = merger.Merge(baseline, local, remote);
        var resolved = merger.Merge(baseline, local, remote, SyncChoice.Cloud);

        Assert.Equal([item.Id], conflict.ConflictingItems);
        Assert.True(resolved.Snapshot.Items[0].IsLiked);
        Assert.Equal(first.Id, RecommendationSet.Primary(resolved.Snapshot.Links.Select(link => link.ToRecommendation()))?.Id);
        Assert.Equal("""{"Commentary":"A cloud correction"}""", resolved.Snapshot.Links[0].EditedMetadata);
        Assert.DoesNotContain(resolved.Snapshot.Links, link => link.IsRemoved == true);
    }
}
