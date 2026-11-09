using System.Text.Json;
using SQLite;
using WatchLists.ExtensionMethods;
using WatchLists.Logger;
using WatchLists.MVVM.Models;
using WatchLists.Services.BackupRestore;
using WatchLists.Services.Enums;
using WatchLists.Services.MuseImport;
using WatchLists.Services.ReliableSync;

namespace WatchLists.Services;

public class WatchListService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly object _databaseLock = new();
    private readonly string _databasePath;
    private readonly WatchListBackupRestorer _backupRestorer;
    private SQLiteConnection? _dbConnection;
    private long _databaseGeneration;
    private bool _disposed;

    public AsyncOperationGate SyncAndImportGate { get; } = new();
    public long DatabaseGeneration => Interlocked.Read(ref _databaseGeneration);

    private SQLiteConnection Database => _dbConnection
        ?? throw new InvalidOperationException("The WatchList database is unavailable. Restart the app to finish recovery.");

    public WatchListService (SettingsService settingsService, string? dbPath = null)
    {
        _settingsService = settingsService;

        _ = FileLogger.WriteLogAsync("WatchListService constructor invoked");

        if (string.IsNullOrWhiteSpace(dbPath))
        {
            string appData;
            try
            {
                appData = FileSystem.AppDataDirectory;
            }
            catch
            {
                appData = Path.Combine(Path.GetTempPath(), "WatchList");
                Directory.CreateDirectory(appData);
            }

            dbPath = Path.Combine(appData, "watchlist.db");
        }

        _databasePath = Path.GetFullPath(dbPath);
        _backupRestorer = new WatchListBackupRestorer(_databasePath);
        _backupRestorer.RecoverIfPending();
        _dbConnection = new SQLiteConnection(_databasePath) { BusyTimeout = TimeSpan.FromSeconds(5) };
        Database.CreateTable<WatchItem>();
        Database.Execute("UPDATE WatchItems SET IsDeleted = 0 WHERE IsDeleted IS NULL;");

        MigrateJsonData(Path.GetDirectoryName(_databasePath) ?? string.Empty);
    }

    private void MigrateJsonData (string appDataDir)
    {
        if (appDataDir.IsEmptyNullOrWhiteSpace()) return;

        var jsonPath = Path.Combine(appDataDir, "watchlist.json");
        if (File.Exists(jsonPath))
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                var jsonItems = JsonSerializer.Deserialize<List<WatchItem>>(json);
                if (jsonItems != null && jsonItems.Count > 0)
                {
                    Database.InsertAll(jsonItems);
                    _ = FileLogger.WriteLogAsync($"Migrated {jsonItems.Count} items from watchlist.json to watchlist.db");
                }
                
                File.Move(jsonPath, jsonPath + ".bak", true);
                _ = FileLogger.WriteLogAsync($"Renamed {jsonPath} to {jsonPath}.bak");
            }
            catch (Exception ex)
            {
                _ = FileLogger.WriteLogAsync($"Error migrating JSON data to SQLite: {ex.Message}");
            }
        }
    }

    public event EventHandler? WatchListChanged;

    public ReliableSyncStore CreateReliableSyncStore(string destination)
        => new(_databasePath, destination);

    public IReadOnlyList<WatchListBackupFile> GetLocalBackups()
    {
        var databaseFolder = Path.GetDirectoryName(_databasePath)!;
        return new[] { "ImportBackups", "SyncBackups", "RestoreBackups" }
            .SelectMany(folder =>
            {
                var path = Path.Combine(databaseFolder, folder);
                return Directory.Exists(path) ? Directory.EnumerateFiles(path, "*.db") : [];
            })
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenBy(path => path, StringComparer.Ordinal)
            .Select(path => new WatchListBackupFile(
                $"{Path.GetFileName(Path.GetDirectoryName(path))} / {Path.GetFileName(path)}", path))
            .ToArray();
    }

    public Task<PreparedWatchListBackup> PrepareBackupAsync(Stream source, CancellationToken cancellationToken = default)
        => _backupRestorer.PrepareAsync(source, cancellationToken);

    public Task<WatchListRestoreReceipt> RestoreBackupAsync(PreparedWatchListBackup backup)
    {
        return SyncAndImportGate.RunAsync(() => Task.Run(() =>
        {
            if (_settingsService.GetAutoSyncEnabled())
                throw new InvalidOperationException("Turn off automatic sync before restoring a backup.");

            WatchListRestoreReceipt result;
            lock (_databaseLock)
            {
                _ = Database;
                result = _backupRestorer.Restore(ref _dbConnection, backup);
                Interlocked.Increment(ref _databaseGeneration);
            }

            NotifyAfterDatabaseReplacement();
            return result;
        }));
    }

    public string CreateReliableSyncBackup()
    {
        var folder = Path.Combine(Path.GetDirectoryName(_databasePath)!, "SyncBackups");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"watchlist-sync-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db");
        lock (_databaseLock)
        {
            Database.Backup(path, "main");
        }
        return path;
    }

    public List<MovieRecommendation> GetRecommendationsForSync()
    {
        lock (_databaseLock)
            return RecommendationStore.ReadExisting(Database);
    }

    public List<WatchItem> GetItemsForImportReview()
    {
        lock (_databaseLock)
            return Database.Table<WatchItem>().ToList();
    }

    public Task<MuseImportReceipt> ApplyMuseImportAsync(MuseImportBatch batch, long? expectedGeneration = null)
    {
        return SyncAndImportGate.RunAsync(() => Task.FromResult(ApplyMuseImport(batch, expectedGeneration)));
    }

    private MuseImportReceipt ApplyMuseImport(MuseImportBatch batch, long? expectedGeneration)
    {
        var folder = Path.GetDirectoryName(_databasePath);
        if (string.IsNullOrWhiteSpace(folder)) throw new InvalidOperationException("The watchlist backup directory is unavailable.");

        MuseImportReceipt receipt;
        lock (_databaseLock)
        {
            if (!IsCurrentGeneration(expectedGeneration))
                throw new InvalidOperationException("The WatchList was restored. Reopen the Muse file and review the matches again.");
            var coordinator = new MuseImportCoordinator(Database, _settingsService.GetAutoSyncEnabled);
            receipt = coordinator.Apply(batch, Path.Combine(folder, "ImportBackups"), DateTime.UtcNow);
        }
        try
        {
            WatchListChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            _ = FileLogger.WriteLogAsync($"Import saved; refresh notification failed: {exception.Message}");
        }

        return receipt;
    }

    public WatchItemSyncResult MergeSyncItems(IEnumerable<WatchItem> items
                                            , IEnumerable<MovieRecommendation> recommendations)
    {
        lock (_databaseLock)
            return new WatchItemSyncMerger(Database).Merge(items, recommendations);
    }

    public List<WatchItem> GetCurrentWatchItems() => GetWatchItems();

    public List<WatchItem> GetWatchItems()
    {
        try
        {
            lock (_databaseLock)
                return Database.Table<WatchItem>().Where(item => !item.IsDeleted).ToList();
        }
        catch (Exception ex)
        {
            _ = FileLogger.WriteLogAsync($"GetWatchItems: Failed to retrieve: {ex.Message}");
            return new List<WatchItem>();
        }
    }

    public List<WatchItem> GetAllWatchItemsIncludingDeleted()
    {
        try
        {
            lock (_databaseLock)
                return Database.Table<WatchItem>().ToList();
        }
        catch (Exception ex)
        {
            _ = FileLogger.WriteLogAsync($"GetAllWatchItemsIncludingDeleted: Failed to retrieve: {ex.Message}");
            return new List<WatchItem>();
        }
    }

    public WatchItem? GetWatchItemById(Guid id)
    {
        if (id == Guid.Empty) return null;
        try
        {
            lock (_databaseLock)
                return Database.Find<WatchItem>(id);
        }
        catch (Exception ex)
        {
            _ = FileLogger.WriteLogAsync($"GetWatchItemById error: {ex.Message}");
            return null;
        }
    }

    public void AddWatchItem (WatchItem item)
    {
        if (item == null) return;

        if (item.Id == Guid.Empty)
        {
            item.Id = Guid.NewGuid();
            _ = FileLogger.WriteLogAsync($"Assigned new ID: {item.Id} to new WatchItem");
        }

        if (item.LastUpdated == default)
        {
            item.LastUpdated = DateTime.UtcNow;
        }
        lock (_databaseLock)
            Database.InsertOrReplace(item);
        _ = FileLogger.WriteLogAsync($"Added new WatchItem with ID: {item.Id} via SQLite");

        WatchListChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool UpdateWatchItem(WatchItem updatedItem, long? expectedGeneration = null)
    {
        if (updatedItem == null) return false;

        lock (_databaseLock)
        {
            if (!IsCurrentGeneration(expectedGeneration)) return false;
            updatedItem.LastUpdated = DateTime.UtcNow;
            Database.InsertOrReplace(updatedItem);
        }
        _ = FileLogger.WriteLogAsync($"Updated WatchItem with ID: {updatedItem.Id} via SQLite");

        WatchListChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void DeleteWatchItem (Guid id)
    {
        try
        {
            lock (_databaseLock)
            {
                var item = Database.Find<WatchItem>(id);
                if (item is null) return;
                item.IsDeleted   = true;
                item.LastUpdated = DateTime.UtcNow;
                Database.InsertOrReplace(item);
            }
            _ = FileLogger.WriteLogAsync($"Soft-deleted WatchItem with ID: {id} via SQLite");
            WatchListChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _ = FileLogger.WriteLogAsync($"DeleteWatchItem error: {ex.Message}");
        }
    }

    public void DeleteWatchItems (IEnumerable<Guid> ids)
    {
        if (ids == null) return;

        try
        {
            var idList = ids.ToList();
            if (idList.Count == 0) return;

            var now = DateTime.UtcNow;
            lock (_databaseLock)
            {
                var connection = Database;
                connection.RunInTransaction(() =>
                {
                    foreach (var id in idList)
                    {
                        var item = connection.Find<WatchItem>(id);
                        if (item is null) continue;
                        item.IsDeleted   = true;
                        item.LastUpdated = now;
                        connection.InsertOrReplace(item);
                    }
                });
            }

            _ = FileLogger.WriteLogAsync($"Soft-deleted {idList.Count} items via batch delete in SQLite");
            WatchListChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _ = FileLogger.WriteLogAsync($"DeleteWatchItems batch error: {ex.Message}");
        }
    }

    public void UpsertWatchItemFromSync (WatchItem item)
    {
        if (item == null) return;

        lock (_databaseLock)
            Database.InsertOrReplace(item);
        _ = FileLogger.WriteLogAsync($"UpsertWatchItemFromSync ID: {item.Id} via SQLite");
    }

    public void NotifyWatchListChanged()
    {
        WatchListChanged?.Invoke(this, EventArgs.Empty);
    }

    public WatchItem? FindDuplicateItem (int movieId, string? apiSource = null, Guid? excludeId = null)
    {
        if (movieId <= 0) return null;

        var items = GetWatchItems();
        return items.FirstOrDefault(item =>
            item.Id != excludeId
         && item.MovieId == movieId
         && (string.IsNullOrWhiteSpace(apiSource)
          || item.ApiSource.IsEmptyNullOrWhiteSpace()
          || item.ApiSource.EqualsIgnoreCase(apiSource)));
    }

    public async Task<bool> SaveWatchItemAsync(WatchItem item, long? expectedGeneration = null)
    {
        if (item == null) return false;
        expectedGeneration ??= DatabaseGeneration;
        if (!IsCurrentGeneration(expectedGeneration)) return false;

        // Check if the category exists in settings options
        var savedCategories = await _settingsService.GetOptionsAsync(SettingType.Categories);

        if (item.Category.HasValue() && savedCategories.DoesNotContain(item.Category))
        {
            savedCategories.Add(item.Category);
            await _settingsService.SaveOptionsAsync(SettingType.Categories
                                                 , savedCategories);
        }

        lock (_databaseLock)
        {
            if (!IsCurrentGeneration(expectedGeneration)) return false;
            item.LastUpdated = DateTime.UtcNow;
            Database.InsertOrReplace(item);
        }
        _ = FileLogger.WriteLogAsync($"Saved WatchItem with ID: {item.Id} via SQLite");

        WatchListChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private bool IsCurrentGeneration(long? expectedGeneration)
        => !expectedGeneration.HasValue || expectedGeneration == DatabaseGeneration;

    private void NotifyAfterDatabaseReplacement()
    {
        try
        {
            NotifyWatchListChanged();
        }
        catch (Exception exception)
        {
            _ = FileLogger.WriteLogAsync($"Restore succeeded; refresh notification failed: {exception.GetType().Name}");
        }
    }

    public void Dispose()
    {
        lock (_databaseLock)
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _dbConnection?.Dispose();
            }
            catch (Exception ex)
            {
                _ = FileLogger.WriteLogAsync($"WatchListService dispose error: {ex.Message}");
            }
            finally
            {
                _dbConnection = null;
            }
        }
    }
}
