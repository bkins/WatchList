using SQLite;
using WatchLists.MVVM.Models;
using WatchLists.Services;
using WatchLists.Services.Restoration;
using WatchLists.Services.MuseImport;
using WatchLists.Services.ReliableSync;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class WatchListBackupRestorerTests
{
    [Fact]
    public async Task RestoreBackupAsync_MuseBackup_ReopensExactRowsAndKeepsSettings()
    {
        using var database = new DisposableImportDatabase();
        var settings = new SettingsService(database.DirectoryPath);
        await settings.SaveAutoSyncEnabledAsync(false);
        await settings.SaveWatchedCategoryAsync("Watched Archive");
        var original = Seed(database);
        var deleted  = new WatchItem { Title = "Deleted title", IsDeleted = true };
        database.Connection.Insert(deleted);
        var before = ReliableSyncStore.ReadLocal(database.Connection);
        var backupPath = database.CreateBackupPath();
        var newItem = new WatchItem { Title = "Later import", MovieId = 99, ApiSource = "TMDB", Category = "New" };
        var batch = new MuseImportBatch([newItem], [new(newItem.Id, "https://example.test/later", "A later recommendation")]);
        new MuseImportWriter(database.Connection).Apply(batch, backupPath, DateTime.UtcNow);
        var afterImport = ReliableSyncStore.ReadLocal(database.Connection);
        var settingsPath = Path.Combine(database.DirectoryPath, "WatchedCategory.json");
        var settingsBytes = await File.ReadAllBytesAsync(settingsPath);
        using var service = new WatchListService(settings, database.DatabasePath);
        var generation = service.DatabaseGeneration;
        var notifications = 0;
        service.WatchListChanged += (_, _) => notifications++;
        using var file = File.OpenRead(backupPath);
        using var prepared = await service.PrepareBackupAsync(file);

        var receipt = await service.RestoreBackupAsync(prepared);
        var staleSave = service.UpdateWatchItem(newItem, generation);

        Assert.False(staleSave);
        Assert.Equal(new WatchListBackupSummary(1, 1, 2, false), receipt.Summary);
        Assert.Equal(generation + 1, service.DatabaseGeneration);
        Assert.Equal(1, notifications);
        Assert.Equal(original.Id, Assert.Single(service.GetWatchItems()).Id);
        using (var rollback = new SQLiteConnection(receipt.RollbackPath, SQLiteOpenFlags.ReadOnly))
            Assert.True(SyncJson.Equal(afterImport, ReliableSyncStore.ReadLocal(rollback)));
        Assert.Equal(settingsBytes, await File.ReadAllBytesAsync(settingsPath));
        Assert.False(settings.GetAutoSyncEnabled());

        service.Dispose();
        using var restarted = new WatchListService(settings, database.DatabasePath);
        using var reopened = new SQLiteConnection(database.DatabasePath, SQLiteOpenFlags.ReadOnly);
        Assert.True(SyncJson.Equal(before, ReliableSyncStore.ReadLocal(reopened)));
        Assert.Equal(original.Id, Assert.Single(restarted.GetWatchItems()).Id);
        Assert.Equal("ok", reopened.ExecuteScalar<string>("PRAGMA integrity_check"));
    }

    [Fact]
    public async Task Restore_BackupIncludesSyncRecovery_PreservesEveryStateRowAndAssociation()
    {
        using var destination = new DisposableImportDatabase();
        using var source = new DisposableImportDatabase();
        var item = Seed(source);
        var snapshot = ReliableSyncStore.ReadLocal(source.Connection);
        var remote = SyncEnvelope.Create(snapshot);
        var conflict = new SyncConflict(Guid.NewGuid(), snapshot, snapshot, snapshot, remote.Revision, "etag", [item.Id]);
        var pending = new SyncPending(remote, snapshot, "etag");
        var sourceStore = new ReliableSyncStore(source.DatabasePath, "test-pairing");
        sourceStore.Transaction((connection, _) =>
        {
            sourceStore.Save(connection, new SyncDestinationState(snapshot, remote.Revision, pending, conflict));
            sourceStore.Archive(connection, conflict.Id.ToString("N"), conflict);
            sourceStore.Archive(connection, remote.Revision, pending);
            return true;
        });
        var settings = new SettingsService(destination.DirectoryPath);
        await settings.SaveAutoSyncEnabledAsync(false);
        var profile = new FirebaseSyncProfile("https://example.firebaseio.com/", "example-api-key"
                                            , "fixture@example.test", Guid.NewGuid().ToString("N"), false);
        await settings.SaveReliableSyncProfileAsync(profile);
        using var service = new WatchListService(settings, destination.DatabasePath);
        using var file = new FileStream(source.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var prepared = await service.PrepareBackupAsync(file);

        var receipt = await service.RestoreBackupAsync(prepared);

        Assert.True(receipt.Summary.HasSyncState);
        Assert.Equal(profile, settings.GetReliableSyncProfile());
        using var reopened = new SQLiteConnection(destination.DatabasePath, SQLiteOpenFlags.ReadOnly);
        Assert.True(SyncJson.Equal(snapshot, ReliableSyncStore.ReadLocal(reopened)));
        Assert.Equal(source.Connection.Table<SyncStateRow>().Single().Data, reopened.Table<SyncStateRow>().Single().Data);
        Assert.Equal(source.Connection.Table<SyncRecoveryRow>().OrderBy(row => row.Id).ToList().Select(row => row.Data)
                   , reopened.Table<SyncRecoveryRow>().OrderBy(row => row.Id).ToList().Select(row => row.Data));
    }

    [Fact]
    public async Task Restore_ReopenFails_ReinstatesPreviousDatabaseAndRetainsRollback()
    {
        using var destination = new DisposableImportDatabase();
        using var source = new DisposableImportDatabase();
        var original = Seed(destination);
        source.Connection.Insert(new WatchItem { Title = "Backup item" });
        var restorer = new WatchListBackupRestorer(destination.DatabasePath);
        using var file = new FileStream(source.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var prepared = await restorer.PrepareAsync(file, CancellationToken.None);
        SQLiteConnection? live = new SQLiteConnection(destination.DatabasePath);
        try
        {
            var error = Assert.Throws<IOException>(() =>
                restorer.Restore(ref live, prepared, () => throw new IOException("Synthetic reopen failure.")));

            Assert.Contains("previous WatchList was reopened", error.Message);
            Assert.Equal(original.Id, Assert.Single(live!.Table<WatchItem>()).Id);
            Assert.Equal(2, RecommendationStore.ReadExisting(live).Count);
            Assert.False(File.Exists(destination.DatabasePath + ".restore-pending"));
            Assert.Single(Directory.GetFiles(Path.Combine(destination.DirectoryPath, "RestoreBackups"), "*.db"));
        }
        finally
        {
            live?.Dispose();
        }
    }

    [Fact]
    public void RecoverIfPending_InterruptedRestore_ReinstatesRollbackBeforeOpening()
    {
        using var destination = new DisposableImportDatabase();
        var original = Seed(destination);
        var id = Guid.NewGuid();
        var folder = Path.Combine(destination.DirectoryPath, "RestoreBackups");
        Directory.CreateDirectory(folder);
        var rollback = Path.Combine(folder, $"watchlist-before-restore-{id:N}.db");
        destination.Connection.Backup(rollback, "main");
        File.WriteAllText(destination.DatabasePath + ".restore-pending", id.ToString("N"));
        destination.Connection.Execute("UPDATE WatchItems SET Title = 'Interrupted copy'");

        using var restarted = new WatchListService(new SettingsService(destination.DirectoryPath), destination.DatabasePath);

        Assert.Equal(original.Title, Assert.Single(restarted.GetWatchItems()).Title);
        Assert.Equal(2, restarted.GetRecommendationsForSync().Count);
        Assert.True(File.Exists(rollback));
        Assert.False(File.Exists(destination.DatabasePath + ".restore-pending"));
    }

    [Fact]
    public void CreateReliableSyncStore_RecoveryPending_BlocksAccessToTheDatabase()
    {
        using var database = new DisposableImportDatabase();
        using var service = new WatchListService(new SettingsService(database.DirectoryPath), database.DatabasePath);
        File.WriteAllText(database.DatabasePath + ".restore-pending", Guid.NewGuid().ToString("N"));

        var exception = Assert.Throws<InvalidOperationException>(() => service.CreateReliableSyncStore("test"));

        Assert.Contains("Restart the app", exception.Message);
    }

    [Theory]
    [InlineData("CREATE TABLE ForeignData (value TEXT)")]
    [InlineData("CREATE TRIGGER unexpected AFTER UPDATE ON WatchItems BEGIN SELECT 1; END")]
    [InlineData("PRAGMA user_version = 99")]
    [InlineData("ALTER TABLE MovieRecommendation ADD COLUMN FutureMetadata TEXT")]
    [InlineData("UPDATE MovieRecommendation SET WatchItemId = '00000000-0000-0000-0000-000000000042'")]
    [InlineData("UPDATE WatchItems SET LastUpdated = 'incompatible-timestamp'")]
    public async Task PrepareAsync_IncompatibleOrOrphanedBackup_RejectsSelection(string sql)
    {
        using var source = new DisposableImportDatabase();
        Seed(source);
        source.Connection.Execute(sql);
        var restorer = new WatchListBackupRestorer(source.DatabasePath);
        using var file = new FileStream(source.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        await Assert.ThrowsAsync<InvalidDataException>(() => restorer.PrepareAsync(file, CancellationToken.None));

        Assert.Equal(1, source.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM WatchItems"));
    }

    [Fact]
    public async Task PrepareAsync_WalMainFileWithoutSidecars_RejectsSelection()
    {
        using var source = new DisposableImportDatabase();
        source.Connection.EnableWriteAheadLogging();
        source.Connection.Insert(new WatchItem { Title = "WAL item" });
        var restorer = new WatchListBackupRestorer(source.DatabasePath);
        using var file = new FileStream(source.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => restorer.PrepareAsync(file, CancellationToken.None));

        Assert.Contains("sidecars", exception.Message);
    }

    [Fact]
    public async Task RestoreBackupAsync_StagedFileChanged_LeavesCurrentListAndSkipsRollbackCreation()
    {
        using var destination = new DisposableImportDatabase();
        using var source = new DisposableImportDatabase();
        var item = Seed(destination);
        source.Connection.Insert(new WatchItem { Title = "Reviewed item" });
        var settings = new SettingsService(destination.DirectoryPath);
        await settings.SaveAutoSyncEnabledAsync(false);
        using var service = new WatchListService(settings, destination.DatabasePath);
        using var file = new FileStream(source.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var prepared = await service.PrepareBackupAsync(file);
        using (var staged = new SQLiteConnection(prepared.Path))
            staged.Execute("UPDATE WatchItems SET Title = 'Changed after review'");

        await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreBackupAsync(prepared));

        Assert.Equal(item.Id, Assert.Single(service.GetWatchItems()).Id);
        Assert.False(Directory.Exists(Path.Combine(destination.DirectoryPath, "RestoreBackups")));
    }

    [Fact]
    public async Task RestoreBackupAsync_AutomaticSyncEnabled_LeavesCurrentDatabase()
    {
        using var destination = new DisposableImportDatabase();
        using var source = new DisposableImportDatabase();
        var item = Seed(destination);
        source.Connection.Insert(new WatchItem { Title = "Backup item" });
        var settings = new SettingsService(destination.DirectoryPath);
        await settings.SaveAutoSyncEnabledAsync(true);
        using var service = new WatchListService(settings, destination.DatabasePath);
        using var file = new FileStream(source.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var prepared = await service.PrepareBackupAsync(file);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreBackupAsync(prepared));

        Assert.Contains("automatic sync", exception.Message);
        Assert.Equal(item.Id, Assert.Single(service.GetWatchItems()).Id);
        Assert.True(settings.GetAutoSyncEnabled());
    }

    [Fact]
    public async Task PrepareBackupAsync_ReviewIsCanceled_RemovesOnlyTheStagedCopy()
    {
        using var destination = new DisposableImportDatabase();
        using var source = new DisposableImportDatabase();
        var original = Seed(destination);
        source.Connection.Insert(new WatchItem { Title = "Backup item" });
        var restorer = new WatchListBackupRestorer(destination.DatabasePath);
        using var file = new FileStream(source.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var reviewed = await restorer.PrepareAsync(file, CancellationToken.None);

        reviewed.Dispose();

        Assert.False(File.Exists(reviewed.Path));
        Assert.Equal(original.Id, Assert.Single(destination.Connection.Table<WatchItem>()).Id);
        Assert.True(File.Exists(source.DatabasePath));
        Assert.False(Directory.Exists(Path.Combine(destination.DirectoryPath, "RestoreBackups")));
    }

    private static WatchItem Seed(DisposableImportDatabase database)
    {
        var item = new WatchItem
                   {
                       Title                      = "Saved before import"
                     , Category                   = "Finished"
                     , PreviousCategory           = "Watch later"
                     , IsWatched                  = true
                     , IsLiked                    = true
                     , MovieId                    = 123
                     , ApiSource                  = "TMDB"
                     , Type                       = "Movie"
                     , StreamingService           = "Netflix"
                     , DeepLinkUri                = "https://example.test/play"
                     , Overview                   = "Synthetic synopsis"
                     , PosterUrl                  = "https://example.test/poster"
                     , AvailableStreamingServices = "Netflix, Hulu"
                     , AggregatedDataJson         = """{"Synthetic":"details"}"""
                   };
        database.Connection.Insert(item);
        new RecommendationStore(database.Connection).Apply(
            [new(item.Id, "https://example.test/first", "First recommendation")
           , new(item.Id, "https://example.test/second", "Second recommendation")], DateTime.UtcNow);
        return item;
    }
}
