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

    public WatchListService (SettingsService settingsService)
    {
        _settingsService = settingsService;

        _ = FileLogger.WriteLogAsync("WatchListService constructor invoked");

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "watchlist.db");
        _dbConnection = new SQLiteConnection(dbPath);
        _dbConnection.CreateTable<WatchItem>();

        MigrateJsonData();
    }

    private void MigrateJsonData()
    {
        var jsonPath = Path.Combine(FileSystem.AppDataDirectory, "watchlist.json");
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

    public List<WatchItem> GetCurrentWatchItems() => GetWatchItems();

    public List<WatchItem> GetWatchItems()
    {
        try
        {
            return _dbConnection.Table<WatchItem>().ToList();
        }
        catch (Exception ex)
        {
            _ = FileLogger.WriteLogAsync($"GetWatchItems: Failed to retrieve: {ex.Message}");
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

        _dbConnection.Insert(item);
        _ = FileLogger.WriteLogAsync($"Added new WatchItem with ID: {item.Id} via SQLite");
    }

    public void UpdateWatchItem (WatchItem updatedItem)
    {
        updatedItem.LastUpdated = DateTime.Now;
        _dbConnection.InsertOrReplace(updatedItem);
        _ = FileLogger.WriteLogAsync($"Updated WatchItem with ID: {updatedItem.Id} via SQLite");
    }

    public void DeleteWatchItem (Guid id)
    {
        _dbConnection.Delete<WatchItem>(id);
        _ = FileLogger.WriteLogAsync($"Deleted WatchItem with ID: {id} via SQLite");
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

        item.LastUpdated = DateTime.Now;
        _dbConnection.InsertOrReplace(item);
        _ = FileLogger.WriteLogAsync($"Saved WatchItem with ID: {item.Id} via SQLite");
    }
}
