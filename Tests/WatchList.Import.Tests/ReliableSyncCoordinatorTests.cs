using SQLite;
using WatchLists.MVVM.Models;
using WatchLists.Services;
using WatchLists.Services.MuseImport;
using WatchLists.Services.ReliableSync;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class ReliableSyncCoordinatorTests
{
    [Fact]
    public async Task SyncAsync_TwoDevicesEditDifferentGroups_MergesAndPersistsRecommendations()
    {
        using var first = new DisposableImportDatabase();
        using var second = new DisposableImportDatabase();
        var item = CreateItem(first);
        var remote = new FakeReliableSyncRemote();
        var firstSync = Coordinator(first, remote);
        var secondSync = Coordinator(second, remote);
        await firstSync.SyncAsync(create: true);
        await secondSync.SyncAsync();
        Update(first, item.Id, saved =>
        {
            saved.Category = "Finished";
            saved.IsWatched = true;
            saved.PreviousCategory = "Queue";
        });
        Update(second, item.Id, saved => saved.IsLiked = true);
        new RecommendationStore(second.Connection).Apply([new(item.Id, "https://example.test/source", "A recommendation")], DateTime.UtcNow);

        await firstSync.SyncAsync();
        var result = await secondSync.SyncAsync();
        await Coordinator(first, remote).SyncAsync();

        Assert.Null(result.Conflict);
        var final = first.Connection.Get<WatchItem>(item.Id);
        Assert.Equal("Finished", final.Category);
        Assert.Equal("Queue", final.PreviousCategory);
        Assert.True(final.IsWatched);
        Assert.True(final.IsLiked);
        Assert.Equal(item.Id, Assert.Single(RecommendationStore.ReadExisting(first.Connection)).WatchItemId);
        Assert.Equal("Sync complete.", result.Message);
    }

    [Fact]
    public async Task SyncAsync_ConflictSurvivesRestart_StaleChoiceRequiresNewReviewAndKeepsIndependentFields()
    {
        using var first = new DisposableImportDatabase();
        using var second = new DisposableImportDatabase();
        var item = CreateItem(first);
        var remote = new FakeReliableSyncRemote();
        var firstSync = Coordinator(first, remote);
        var secondSync = Coordinator(second, remote);
        await firstSync.SyncAsync(create: true);
        await secondSync.SyncAsync();
        Update(first, item.Id, saved =>
        {
            saved.Category = "Local choice";
            saved.IsLiked = true;
        });
        Update(second, item.Id, saved => saved.Category = "Cloud choice");
        await secondSync.SyncAsync();
        var initial = (await firstSync.SyncAsync()).Conflict!;
        Assert.Equal(initial.Id, new ReliableSyncStore(first.DatabasePath, "test").GetConflict()!.Id);
        Update(second, item.Id, saved => saved.Category = "Newer cloud choice");
        await secondSync.SyncAsync();

        var restarted = Coordinator(first, remote);
        var stale = await restarted.SyncAsync(resolution: new SyncResolution(initial.Id, SyncChoice.Local));
        Assert.NotNull(stale.Conflict);
        Assert.NotEqual(initial.Id, stale.Conflict.Id);
        Assert.Equal("Newer cloud choice", Assert.Single(stale.Conflict.Remote.Items).Category);

        var result = await restarted.SyncAsync(resolution: new SyncResolution(stale.Conflict.Id, SyncChoice.Cloud));

        Assert.Null(result.Conflict);
        var final = first.Connection.Get<WatchItem>(item.Id);
        Assert.Equal("Newer cloud choice", final.Category);
        Assert.True(final.IsLiked);
        Assert.Equal(2, first.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM ReliableSyncRecovery"));
    }

    [Fact]
    public async Task SyncAsync_DeleteVersusEdit_RetainsTombstoneAndOriginalRecommendations()
    {
        using var first = new DisposableImportDatabase();
        using var second = new DisposableImportDatabase();
        var item = CreateItem(first);
        new RecommendationStore(first.Connection).Apply([new(item.Id, "https://example.test/source", "A recommendation")], DateTime.UtcNow);
        var remote = new FakeReliableSyncRemote();
        await Coordinator(first, remote).SyncAsync(create: true);
        await Coordinator(second, remote).SyncAsync();
        Update(first, item.Id, saved => saved.IsDeleted = true);
        Update(second, item.Id, saved => saved.Title = "Edited title");
        await Coordinator(second, remote).SyncAsync();

        var conflict = (await Coordinator(first, remote).SyncAsync()).Conflict!;
        Assert.True(Assert.Single(conflict.Local.Items).IsDeleted);
        Assert.Equal("Edited title", Assert.Single(conflict.Remote.Items).Title);

        await Coordinator(first, remote).SyncAsync(resolution: new SyncResolution(conflict.Id, SyncChoice.Local));
        await Coordinator(second, remote).SyncAsync();

        Assert.True(second.Connection.Get<WatchItem>(item.Id).IsDeleted);
        Assert.Equal(item.Id, Assert.Single(RecommendationStore.ReadExisting(second.Connection)).WatchItemId);
    }

    [Fact]
    public async Task SyncAsync_ConcurrentWrites_RereadsInsteadOfOverwriting()
    {
        using var database = new DisposableImportDatabase();
        var item = CreateItem(database);
        var remote = new FakeReliableSyncRemote();
        var sync = Coordinator(database, remote);
        await sync.SyncAsync(create: true);
        Update(database, item.Id, saved => saved.IsLiked = true);
        remote.BeforeWrite = () =>
        {
            var original = Assert.Single(remote.Current!.Decode().Items);
            remote.Current = SyncEnvelope.Create(new SyncSnapshot([original with { Category = "Other device" }], []));
            remote.BeforeWrite = null;
        };

        await sync.SyncAsync();

        Assert.Equal(1, remote.RejectedWrites);
        var merged = Assert.Single(remote.Current!.Decode().Items);
        Assert.True(merged.IsLiked);
        Assert.Equal("Other device", merged.Category);
    }

    [Fact]
    public async Task SyncAsync_ContinuousContention_StopsAfterThreeWritesWithLocalEditIntact()
    {
        using var database = new DisposableImportDatabase();
        var item = CreateItem(database);
        var remote = new FakeReliableSyncRemote();
        var sync = Coordinator(database, remote);
        await sync.SyncAsync(create: true);
        Update(database, item.Id, saved => saved.IsLiked = true);
        remote.BeforeWrite = () =>
        {
            var current = Assert.Single(remote.Current!.Decode().Items);
            remote.Current = SyncEnvelope.Create(new SyncSnapshot([current with { Category = Guid.NewGuid().ToString("N") }], []));
        };

        var result = await sync.SyncAsync();

        Assert.Contains("kept updating", result.Message);
        Assert.Equal(3, remote.RejectedWrites);
        Assert.True(database.Connection.Get<WatchItem>(item.Id).IsLiked);
        Assert.False(Assert.Single(remote.Current!.Decode().Items).IsLiked);
    }

    [Fact]
    public async Task SyncAsync_UnknownAcknowledgementAndRestart_DoesNotResendOrDiscardLaterEdit()
    {
        using var database = new DisposableImportDatabase();
        var item = CreateItem(database);
        var remote = new FakeReliableSyncRemote { LoseAcknowledgement = true };

        await Coordinator(database, remote).SyncAsync(create: true);
        Assert.NotNull(new ReliableSyncStore(database.DatabasePath, "test").Transaction((_, state) => state.Pending));
        Update(database, item.Id, saved => saved.IsLiked = true);
        remote.LoseAcknowledgement = false;
        var result = await Coordinator(database, remote).SyncAsync();

        Assert.Equal("Sync complete.", result.Message);
        Assert.Equal(2, remote.WriteCount);
        Assert.True(Assert.Single(remote.Current!.Decode().Items).IsLiked);
        Assert.Null(new ReliableSyncStore(database.DatabasePath, "test").Transaction((_, state) => state.Pending));
    }

    [Fact]
    public async Task SyncAsync_LocalEditDuringSuccessfulWrite_PreservesTheEditAndItsCloudAlternative()
    {
        using var first = new DisposableImportDatabase();
        using var second = new DisposableImportDatabase();
        var item = CreateItem(first);
        var remote = new FakeReliableSyncRemote();
        await Coordinator(first, remote).SyncAsync(create: true);
        await Coordinator(second, remote).SyncAsync();
        Update(second, item.Id, saved => saved.Category = "Cloud");
        await Coordinator(second, remote).SyncAsync();
        Update(first, item.Id, saved => saved.IsLiked = true);
        remote.BeforeWrite = () => Update(first, item.Id, saved => saved.Category = "Edited during the write");

        var result = await Coordinator(first, remote).SyncAsync();

        Assert.NotNull(result.Conflict);
        Assert.Equal("Edited during the write", first.Connection.Get<WatchItem>(item.Id).Category);
        Assert.Equal("Cloud", Assert.Single(result.Conflict.Remote.Items).Category);
        Assert.True(Assert.Single(result.Conflict.Remote.Items).IsLiked);
    }

    [Fact]
    public async Task SyncAsync_CancelAfterPrepare_RestartReconcilesUnsentChanges()
    {
        using var database = new DisposableImportDatabase();
        var item = CreateItem(database);
        var remote = new FakeReliableSyncRemote();
        using var cancellation = new CancellationTokenSource();
        remote.BeforeWrite = cancellation.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Coordinator(database, remote).SyncAsync(create: true, cancellationToken: cancellation.Token));
        Assert.Null(remote.Current);
        Assert.NotNull(new ReliableSyncStore(database.DatabasePath, "test").Transaction((_, state) => state.Pending));
        remote.BeforeWrite = null;

        await Coordinator(database, remote).SyncAsync();

        Assert.Equal(item.Id, Assert.Single(remote.Current!.Decode().Items).Id);
        Assert.Null(new ReliableSyncStore(database.DatabasePath, "test").Transaction((_, state) => state.Pending));
    }

    [Fact]
    public async Task SyncAsync_BaselineFreeProviderCollision_RequiresDeliberateChoice()
    {
        using var first = new DisposableImportDatabase();
        using var second = new DisposableImportDatabase();
        var cloudItem = CreateItem(first);
        var localItem = CreateItem(second);
        var remote = new FakeReliableSyncRemote();
        await Coordinator(first, remote).SyncAsync(create: true);

        var conflict = (await Coordinator(second, remote).SyncAsync()).Conflict!;

        Assert.Contains(cloudItem.Id, conflict.ItemIds);
        Assert.Contains(localItem.Id, conflict.ItemIds);
        Assert.Equal(1, remote.WriteCount);
        await Coordinator(second, remote).SyncAsync(resolution: new SyncResolution(conflict.Id, SyncChoice.Cloud));
        Assert.True(second.Connection.Get<WatchItem>(localItem.Id).IsDeleted);
        Assert.False(second.Connection.Get<WatchItem>(cloudItem.Id).IsDeleted);
    }

    [Fact]
    public async Task Backup_ConflictAndPendingVersions_RestoreWithItemsAndLinks()
    {
        using var first = new DisposableImportDatabase();
        using var second = new DisposableImportDatabase();
        var item = CreateItem(first);
        var remote = new FakeReliableSyncRemote();
        await Coordinator(first, remote).SyncAsync(create: true);
        await Coordinator(second, remote).SyncAsync();
        Update(first, item.Id, saved => saved.Category = "Local");
        Update(second, item.Id, saved => saved.Category = "Remote");
        new RecommendationStore(first.Connection).Apply([new(item.Id, "https://example.test/source", "Evidence")], DateTime.UtcNow);
        await Coordinator(second, remote).SyncAsync();
        var conflict = (await Coordinator(first, remote).SyncAsync()).Conflict!;
        remote.LoseAcknowledgement = true;
        await Coordinator(first, remote).SyncAsync(resolution: new SyncResolution(conflict.Id, SyncChoice.Local));
        using var service = new WatchListService(new SettingsService(first.DirectoryPath), first.DatabasePath);

        var backupPath = service.CreateReliableSyncBackup();
        using var restored = new SQLiteConnection(backupPath);
        var state = new ReliableSyncStore(backupPath, "test").Transaction((_, saved) => saved);

        Assert.Equal("ok", restored.ExecuteScalar<string>("PRAGMA integrity_check"));
        Assert.Equal(conflict.Id, state.Conflict!.Id);
        Assert.NotNull(state.Pending);
        Assert.Equal(item.Id, Assert.Single(state.Baseline.Items).Id);
        Assert.Equal(item.Id, Assert.Single(restored.Table<WatchItem>()).Id);
        Assert.Equal(item.Id, Assert.Single(RecommendationStore.ReadExisting(restored)).WatchItemId);
        Assert.Equal(1, restored.ExecuteScalar<int>("SELECT COUNT(*) FROM ReliableSyncRecovery"));
    }

    [Fact]
    public async Task SyncAsync_MissingOrOldDestination_LeavesLocalDataAndCloudUnmodified()
    {
        using var database = new DisposableImportDatabase();
        var item = CreateItem(database);
        var remote = new FakeReliableSyncRemote();
        var sync = Coordinator(database, remote);

        var missing = await sync.SyncAsync();
        remote.Current = new SyncEnvelope(2, Guid.NewGuid().ToString("N"), "{}");
        await Assert.ThrowsAsync<InvalidDataException>(() => sync.SyncAsync());

        Assert.Contains("empty or missing", missing.Message);
        Assert.Equal(0, remote.WriteCount);
        Assert.Equal(item.Id, Assert.Single(database.Connection.Table<WatchItem>()).Id);
    }

    [Fact]
    public async Task SyncAsync_TwoDestinations_KeepTheirBaselinesSeparate()
    {
        using var database = new DisposableImportDatabase();
        var item = CreateItem(database);
        var firstRemote = new FakeReliableSyncRemote();
        var secondRemote = new FakeReliableSyncRemote();
        var firstStore = new ReliableSyncStore(database.DatabasePath, "first");
        var secondStore = new ReliableSyncStore(database.DatabasePath, "second");
        await new ReliableSyncCoordinator(firstStore, firstRemote).SyncAsync(create: true);
        Update(database, item.Id, saved => saved.Category = "Second pairing");

        await new ReliableSyncCoordinator(secondStore, secondRemote).SyncAsync(create: true);

        var firstBaseline = firstStore.Transaction((_, state) => state.Baseline);
        var secondBaseline = secondStore.Transaction((_, state) => state.Baseline);
        Assert.Equal("Queue", Assert.Single(firstBaseline.Items).Category);
        Assert.Equal("Second pairing", Assert.Single(secondBaseline.Items).Category);
    }

    private static ReliableSyncCoordinator Coordinator(DisposableImportDatabase database, FakeReliableSyncRemote remote)
        => new(new ReliableSyncStore(database.DatabasePath, "test"), remote);

    private static WatchItem CreateItem(DisposableImportDatabase database)
    {
        var item = new WatchItem
                   {
                       Title    = "Synthetic film"
                     , Category = "Queue"
                     , MovieId  = 42
                     , ApiSource = "TMDB"
                     , Type     = "Movie"
                   };
        database.Connection.Insert(item);
        return item;
    }

    private static void Update(DisposableImportDatabase database, Guid id, Action<WatchItem> edit)
    {
        var saved = database.Connection.Get<WatchItem>(id);
        edit(saved);
        database.Connection.Update(saved);
    }
}
