using System.Text.Json;
using SQLite;
using WatchLists.ExtensionMethods;
using WatchLists.Logger;
using WatchLists.MVVM.Models;
using WatchLists.Services.Enums;

namespace WatchLists.Services;

public class WatchListService
{
    private readonly SettingsService _settingsService;
    private readonly SQLiteConnection _dbConnection;

    public WatchListService (SettingsService settingsService, string? dbPath = null)
    {
        _settingsService = settingsService;

        _ = FileLogger.WriteLogAsync("WatchListService constructor invoked");

        if (dbPath.IsEmptyNullOrWhiteSpace())
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

        _dbConnection = new SQLiteConnection(dbPath);
        _dbConnection.CreateTable<WatchItem>();
        _dbConnection.Execute("UPDATE WatchItems SET IsDeleted = 0 WHERE IsDeleted IS NULL;");

        MigrateJsonData(Path.GetDirectoryName(dbPath) ?? string.Empty);
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
                    _dbConnection.InsertAll(jsonItems);
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

    public List<WatchItem> GetCurrentWatchItems() => GetWatchItems();

    public List<WatchItem> GetWatchItems()
    {
        try
        {
            return _dbConnection.Table<WatchItem>().Where(item => !item.IsDeleted).ToList();
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
            return _dbConnection.Table<WatchItem>().ToList();
        }
        catch (Exception ex)
        {
            _ = FileLogger.WriteLogAsync($"GetAllWatchItemsIncludingDeleted: Failed to retrieve: {ex.Message}");
            return new List<WatchItem>();
        }
    }

    public void AddWatchItem (WatchItem item)
    {
        if (item.Id == Guid.Empty)
        {
            item.Id = Guid.NewGuid();
            _ = FileLogger.WriteLogAsync($"Assigned new ID: {item.Id} to new WatchItem");
        }

        if (item.LastUpdated == default)
        {
            item.LastUpdated = DateTime.UtcNow;
        }
        _dbConnection.InsertOrReplace(item);
        _ = FileLogger.WriteLogAsync($"Added new WatchItem with ID: {item.Id} via SQLite");

        WatchListChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateWatchItem (WatchItem updatedItem)
    {
        updatedItem.LastUpdated = DateTime.UtcNow;
        _dbConnection.InsertOrReplace(updatedItem);
        _ = FileLogger.WriteLogAsync($"Updated WatchItem with ID: {updatedItem.Id} via SQLite");

        WatchListChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteWatchItem (Guid id)
    {
        try
        {
            var item = _dbConnection.Find<WatchItem>(id);
            if (item != null)
            {
                item.IsDeleted   = true;
                item.LastUpdated = DateTime.UtcNow;
                _dbConnection.InsertOrReplace(item);
                _ = FileLogger.WriteLogAsync($"Soft-deleted WatchItem with ID: {id} via SQLite");

                WatchListChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _ = FileLogger.WriteLogAsync($"DeleteWatchItem error: {ex.Message}");
        }
    }

    public void UpsertWatchItemFromSync (WatchItem item)
    {
        _dbConnection.InsertOrReplace(item);
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
         && (apiSource.IsEmptyNullOrWhiteSpace()
          || item.ApiSource.IsEmptyNullOrWhiteSpace()
          || item.ApiSource.EqualsIgnoreCase(apiSource)));
    }

    public async Task SaveWatchItemAsync (WatchItem item)
    {
        // Check if the category exists in settings options
        var savedCategories = await _settingsService.GetOptionsAsync(SettingType.Categories);

        if (savedCategories.DoesNotContain(item.Category))
        {
            savedCategories.Add(item.Category);
            await _settingsService.SaveOptionsAsync(SettingType.Categories
                                                 , savedCategories);
        }

        item.LastUpdated = DateTime.UtcNow;
        _dbConnection.InsertOrReplace(item);
        _ = FileLogger.WriteLogAsync($"Saved WatchItem with ID: {item.Id} via SQLite");

        WatchListChanged?.Invoke(this, EventArgs.Empty);
    }
}
