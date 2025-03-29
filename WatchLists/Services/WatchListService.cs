using System.Text.Json;
using WatchLists.MVVM.Models;
using Microsoft.Extensions.Logging;
using WatchLists.ExtensionMethods;
using WatchLists.Logger;
using WatchLists.Services.Enums;

namespace WatchLists.Services;

public class WatchListService
{
    private readonly SettingsService           _settingsService;
    // private readonly ILogger<WatchListService> _logger;
    private static   WatchListService?         _instance;
    private          List<WatchItem>           _watchItems;

    private const string WatchListFileName  = "watchlist.json";
    private const string CategoriesFileName = "Categories.json";

    // For example, a simple getter for testing:
    public List<WatchItem> GetCurrentWatchItems() => _watchItems;

    public WatchListService(SettingsService settingsService/*, ILogger<WatchListService> logger*/)
    {
        _settingsService = settingsService;

        _ = FileLogger.WriteLogAsync("WatchListService constructor invoked");
        LoadData();
    }

    private void LoadData()
    {
        var filePath = GetWatchListFilePath();

        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(GetWatchListFilePath());
            _watchItems = JsonSerializer.Deserialize<List<WatchItem>>(json) ?? new List<WatchItem>();

            _ = FileLogger.WriteLogAsync($"Loaded {_watchItems.Count} watch items");
        }
        else
        {
            _ = FileLogger.WriteLogAsync($"File path not found: {filePath}");
            _watchItems = new List<WatchItem>();
        }
    }

    private string GetWatchListFilePath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder
                                                                 .LocalApplicationData)
                          , WatchListFileName);
    }

    private void SaveData()
    {
        try
        {
            var json = JsonSerializer.Serialize(_watchItems
                                              , new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetWatchListFilePath()
                            , json);
            _ = FileLogger.WriteLogAsync($"Saved {_watchItems.Count} watch items");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            _ = FileLogger.WriteLogAsync($"SaveData: Failed to save data: {e.Message}");
            throw;
        }
    }

    public async Task SaveWatchItemAsync(WatchItem item)
    {
        var watchItems = GetWatchItems(); // Get existing items

        // Check if the category exists in JSON
        var savedCategories = await _settingsService.GetOptionsAsync(SettingType.Categories);

        if (savedCategories.DoesNotContain(item.Category))
        {
            savedCategories.Add(item.Category);
            await _settingsService.SaveOptionsAsync(SettingType.Categories
                                                  , savedCategories);
        }

        // Save or update the WatchItem
        if (watchItems.All(watchItem => watchItem.Id != item.Id))
        {
            watchItems.Add(item);
        }
        else
        {
            var existingItem = watchItems.First(watchItem => watchItem.Id == item.Id);
            existingItem.Title       = item.Title;
            existingItem.Category    = item.Category;
            existingItem.DeepLinkUri = item.DeepLinkUri;
        }

        await SaveWatchItems(watchItems); // Your existing method to persist items
    }

    private async Task SaveWatchItems(List<WatchItem> watchItems)
    {
        var filePath = GetWatchListFilePath();
        var json     = JsonSerializer.Serialize(watchItems);
        await File.WriteAllTextAsync(filePath
                                   , json);
    }

    public List<WatchItem> GetWatchItems()
    {
        // Reload data to ensure fresh read from file
        LoadData();

        return _watchItems;
    }

    public void AddWatchItem (WatchItem item)
    {
        if (item.Id == Guid.Empty)
        {
            item.Id = Guid.NewGuid();

            _ = FileLogger.WriteLogAsync($"Assigned new ID: {item.Id} to new WatchItem");
        }

        _watchItems.Add(item);
        _ = FileLogger.WriteLogAsync($"Added new WatchItem with ID: {item.Id}");

        SaveData();
    }


    public void UpdateWatchItem (WatchItem updatedItem)
    {
        var existingItem = _watchItems.FirstOrDefault(w => w.Id == updatedItem.Id);

        if (existingItem != null)
        {
            // Update fields
            existingItem.Title            = updatedItem.Title;
            existingItem.StreamingService = updatedItem.StreamingService;
            existingItem.Category         = updatedItem.Category;
            existingItem.DeepLinkUri      = updatedItem.DeepLinkUri;
            existingItem.LastUpdated      = DateTime.Now;
            existingItem.IsWatched        = updatedItem.IsWatched;
            existingItem.IsLiked          = updatedItem.IsLiked;
            existingItem.Type             = updatedItem.Type;

            _ = FileLogger.WriteLogAsync($"Updated WatchItem with ID: {updatedItem.Id}");
        }
        else
        {
            AddWatchItem(updatedItem);
        }

        SaveData();
    }


    public void DeleteWatchItem(Guid id)
    {
        _watchItems.RemoveAll(x => x.Id == id);
        SaveData();
    }
}
