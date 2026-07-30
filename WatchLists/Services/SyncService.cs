using System.Text.Json;
using WatchLists.ExtensionMethods;
using WatchLists.Logger;
using WatchLists.MVVM.Models;
using WatchLists.Services.Enums;
using WatchLists.Services.Interfaces;
using WatchLists.Services.Models;

namespace WatchLists.Services;

public class SyncService
{
    private readonly WatchListService    _watchListService;
    private readonly SettingsService     _settingsService;
    private readonly IRemoteSyncProvider _remoteSyncProvider;
    private readonly SemaphoreSlim        _exportLock = new(1, 1);

    public SyncService (WatchListService       watchListService
                      , SettingsService        settingsService
                      , IRemoteSyncProvider?   remoteSyncProvider = null)
    {
        _watchListService   = watchListService;
        _settingsService    = settingsService;
        _remoteSyncProvider = remoteSyncProvider ?? new CloudApiSyncProvider();

        _watchListService.WatchListChanged += OnWatchListChanged;
    }

    private void OnWatchListChanged (object? sender, EventArgs eventArgs)
    {
        if (_settingsService.GetAutoSyncEnabled())
        {
            _ = Task.Run(async () =>
            {
                await ExportSyncBundleAsync();
            });
        }
    }

    public string GetDeviceSyncFileName()
    {
        var deviceName = DeviceInfo.Name;
        if (deviceName.IsEmptyNullOrWhiteSpace())
        {
            deviceName = Environment.MachineName;
        }

        if (deviceName.IsEmptyNullOrWhiteSpace())
        {
            deviceName = "Device";
        }

        var safeName = string.Concat(deviceName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
        return $"watchlist_sync_{safeName}.json";
    }

    public async Task<string?> CreateNewCloudSyncEndpointAsync()
    {
        var endpointUrl = await _remoteSyncProvider.CreateNewCloudSyncBlobAsync();
        if (endpointUrl.HasValue())
        {
            await _settingsService.SaveApiEndpointUrlAsync(endpointUrl);
            await ExportSyncBundleInternalAsync();
        }
        return endpointUrl;
    }

    public async Task<bool> ExportSyncBundleAsync()
    {
        await _exportLock.WaitAsync();
        try
        {
            return await ExportSyncBundleInternalAsync();
        }
        finally
        {
            _exportLock.Release();
        }
    }

    private async Task<bool> ExportSyncBundleInternalAsync()
    {
        try
        {
            var mode = _settingsService.GetSyncMode();

            var bundle = new SyncBundle
                         {
                             ExportedAtUtc     = DateTime.UtcNow
                           , DeviceId          = DeviceInfo.Name
                           , Items             = _watchListService.GetAllWatchItemsIncludingDeleted()
                           , Categories        = await _settingsService.GetOptionsAsync(SettingType.Categories)
                           , StreamingServices = await _settingsService.GetOptionsAsync(SettingType.StreamingServices)
                           , Types             = await _settingsService.GetOptionsAsync(SettingType.Types)
                           , WatchedCategory   = await _settingsService.GetWatchedCategoryAsync()
                         };

            if (mode.EqualsIgnoreCase("CloudApi"))
            {
                var url      = _settingsService.GetApiEndpointUrl();
                var syncCode = _settingsService.GetSyncCode();

                if (url.IsEmptyNullOrWhiteSpace() && syncCode.HasValue())
                {
                    url = $"https://jsonblob.com/api/jsonBlob/{syncCode}";
                    await _settingsService.SaveApiEndpointUrlAsync(url);
                }

                if (url.IsEmptyNullOrWhiteSpace() || syncCode.IsEmptyNullOrWhiteSpace())
                {
                    await FileLogger.WriteLogAsync("ExportSyncBundleAsync CloudApi error: Endpoint URL or Sync Code is empty.");
                    return false;
                }

                var success = await _remoteSyncProvider.UploadBundleAsync(url, syncCode, bundle);
                if (! success && (url.Contains("firebaseio.com", StringComparison.OrdinalIgnoreCase) || ! url.StartsWith("https://jsonblob.com", StringComparison.OrdinalIgnoreCase)))
                {
                    await FileLogger.WriteLogAsync($"ExportSyncBundleAsync: Cloud endpoint '{url}' failed. Auto-provisioning live cloud endpoint...");
                    var newUrl = await _remoteSyncProvider.CreateNewCloudSyncBlobAsync();
                    if (newUrl.HasValue())
                    {
                        await _settingsService.SaveApiEndpointUrlAsync(newUrl);
                        success = await _remoteSyncProvider.UploadBundleAsync(newUrl, syncCode, bundle);
                    }
                }

                if (success)
                {
                    await FileLogger.WriteLogAsync($"ExportSyncBundleAsync CloudApi: Successfully uploaded {bundle.Items.Count} items.");
                }
                return success;
            }
            else
            {
                var syncFolder = _settingsService.GetSyncFolderPath();
                if (syncFolder.IsEmptyNullOrWhiteSpace() || ! Directory.Exists(syncFolder))
                {
                    await FileLogger.WriteLogAsync("ExportSyncBundleAsync: Sync folder path is not set or directory does not exist.");
                    return false;
                }

                var json           = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true });
                var deviceFileName = GetDeviceSyncFileName();
                var filePath       = Path.Combine(syncFolder, deviceFileName);

                await File.WriteAllTextAsync(filePath, json);
                await FileLogger.WriteLogAsync($"ExportSyncBundleAsync: Exported {bundle.Items.Count} items to {filePath}");
                return true;
            }
        }
        catch (Exception ex)
        {
            await FileLogger.WriteLogAsync($"ExportSyncBundleAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<string> ImportAndMergeSyncBundleAsync()
    {
        var mode = _settingsService.GetSyncMode();

        if (mode.EqualsIgnoreCase("CloudApi"))
        {
            var url      = _settingsService.GetApiEndpointUrl();
            var syncCode = _settingsService.GetSyncCode();

            if (url.IsEmptyNullOrWhiteSpace() || syncCode.IsEmptyNullOrWhiteSpace())
            {
                return "Cloud API Sync error: Endpoint URL or Sync Code is not configured.";
            }

            var bundle = await _remoteSyncProvider.FetchLatestBundleAsync(url, syncCode);
            if (bundle == null)
            {
                await FileLogger.WriteLogAsync($"Cloud API Sync: No existing remote payload found for '{syncCode}'. Uploading local dataset to initialize cloud.");
                var exported = await ExportSyncBundleAsync();
                if (exported)
                {
                    var itemCount = _watchListService.GetWatchItems().Count;
                    var initMsg   = $"Cloud payload initialized! Uploaded {itemCount} items to cloud.";
                    await FileLogger.WriteLogAsync(initMsg);
                    return initMsg;
                }

                var newUrl = await CreateNewCloudSyncEndpointAsync();
                if (newUrl.HasValue())
                {
                    var itemCount = _watchListService.GetWatchItems().Count;
                    var autoMsg   = $"Cloud storage created! Uploaded {itemCount} items to live cloud URL: {newUrl}";
                    await FileLogger.WriteLogAsync(autoMsg);
                    return autoMsg;
                }

                return $"Cloud API Sync error: No remote payload found for '{syncCode}' and failed to initialize cloud.";
            }

            var (insertedCount, updatedCount) = await MergeBundleIntoLocalDbAsync(bundle);
            if (insertedCount > 0 || updatedCount > 0)
            {
                _watchListService.NotifyWatchListChanged();
            }

            var activeCount = _watchListService.GetWatchItems().Count;
            var cloudMsg    = $"Cloud API Sync complete: {insertedCount} inserted, {updatedCount} updated. Active items in DB: {activeCount}.";
            await FileLogger.WriteLogAsync(cloudMsg);
            return cloudMsg;
        }

        var syncFolder = _settingsService.GetSyncFolderPath();
        if (syncFolder.IsEmptyNullOrWhiteSpace() || ! Directory.Exists(syncFolder))
        {
            return "Sync folder path is not configured or available.";
        }

        var files = Directory.GetFiles(syncFolder, "watchlist_sync*.json");
        if (files.Length == 0)
        {
            return "No sync payload files found in configured sync directory.";
        }

        var totalInsertedCount  = 0;
        var totalUpdatedCount   = 0;
        var processedFilesCount = 0;

        foreach (var filePath in files)
        {
            try
            {
                var json   = await File.ReadAllTextAsync(filePath);
                var bundle = JsonSerializer.Deserialize<SyncBundle>(json);

                if (bundle == null) continue;

                var (inserted, updated) = await MergeBundleIntoLocalDbAsync(bundle);
                totalInsertedCount     += inserted;
                totalUpdatedCount      += updated;
                processedFilesCount++;

                if (Path.GetFileName(filePath).Contains("conflict", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(filePath);
                        await FileLogger.WriteLogAsync($"Cleaned up conflict file: {filePath}");
                    }
                    catch
                    {
                        // Ignore deletion error if file is locked
                    }
                }
            }
            catch (Exception ex)
            {
                await FileLogger.WriteLogAsync($"Error importing {filePath}: {ex.Message}");
            }
        }

        if (totalInsertedCount > 0 || totalUpdatedCount > 0)
        {
            _watchListService.NotifyWatchListChanged();
        }

        var activeItemsCount = _watchListService.GetWatchItems().Count;
        var resultMsg        = $"Sync complete across {processedFilesCount} files: {totalInsertedCount} inserted, {totalUpdatedCount} updated. Active items: {activeItemsCount}.";
        await FileLogger.WriteLogAsync(resultMsg);
        return resultMsg;
    }

    private async Task<(int Inserted, int Updated)> MergeBundleIntoLocalDbAsync (SyncBundle bundle)
    {
        var insertedCount = 0;
        var updatedCount  = 0;
        var localItems    = _watchListService.GetAllWatchItemsIncludingDeleted();

        if (bundle.Items != null && bundle.Items.Count > 0)
        {
            foreach (var incomingItem in bundle.Items)
            {
                var localMatch = FindLocalMatch(incomingItem, localItems);

                if (localMatch == null)
                {
                    _watchListService.UpsertWatchItemFromSync(incomingItem);
                    localItems.Add(incomingItem);
                    insertedCount++;
                    await FileLogger.WriteLogAsync($"Sync inserted new item: '{incomingItem.Title}' (ID: {incomingItem.Id})");
                }
                else
                {
                    var incomingUtc = ToUniversalUtc(incomingItem.LastUpdated);
                    var localUtc    = ToUniversalUtc(localMatch.LastUpdated);

                    if (incomingUtc > localUtc)
                    {
                        incomingItem.Id = localMatch.Id;
                        _watchListService.UpsertWatchItemFromSync(incomingItem);

                        var index = localItems.IndexOf(localMatch);
                        if (index >= 0) localItems[index] = incomingItem;

                        updatedCount++;
                        await FileLogger.WriteLogAsync($"Sync updated item: '{incomingItem.Title}' (Remote UTC: {incomingUtc} > Local UTC: {localUtc})");
                    }
                }
            }
        }

        await MergeOptionsAsync(SettingType.Categories, bundle.Categories);
        await MergeOptionsAsync(SettingType.StreamingServices, bundle.StreamingServices);
        await MergeOptionsAsync(SettingType.Types, bundle.Types);

        if (bundle.WatchedCategory.HasValue())
        {
            var localWatched = await _settingsService.GetWatchedCategoryAsync();
            if (localWatched.IsEmptyNullOrWhiteSpace())
            {
                await _settingsService.SaveWatchedCategoryAsync(bundle.WatchedCategory);
            }
        }

        return (insertedCount, updatedCount);
    }

    private static WatchItem? FindLocalMatch (WatchItem incoming, List<WatchItem> localItems)
    {
        // 1. Match by exact GUID Id
        var match = localItems.FirstOrDefault(item => item.Id == incoming.Id);
        if (match != null) return match;

        // 2. Match by MovieId (if non-zero)
        if (incoming.MovieId > 0)
        {
            match = localItems.FirstOrDefault(item => item.MovieId == incoming.MovieId &&
                                                      (item.ApiSource.IsEmptyNullOrWhiteSpace() || incoming.ApiSource.IsEmptyNullOrWhiteSpace() || item.ApiSource.EqualsIgnoreCase(incoming.ApiSource)));
            if (match != null) return match;
        }

        // 3. Match by Title + Type (case-insensitive)
        if (incoming.Title.HasValue())
        {
            match = localItems.FirstOrDefault(item => item.Title.EqualsIgnoreCase(incoming.Title) &&
                                                      (item.Type.IsEmptyNullOrWhiteSpace() || incoming.Type.IsEmptyNullOrWhiteSpace() || item.Type.EqualsIgnoreCase(incoming.Type)));
            if (match != null) return match;
        }

        return null;
    }

    private async Task MergeOptionsAsync (SettingType settingType, List<string>? incomingOptions)
    {
        if (incomingOptions == null || incomingOptions.Count == 0) return;

        var localOptions = await _settingsService.GetOptionsAsync(settingType);
        var changed      = false;

        foreach (var option in incomingOptions)
        {
            if (option.HasValue() && localOptions.DoesNotContain(option))
            {
                localOptions.Add(option);
                changed = true;
            }
        }

        if (changed)
        {
            await _settingsService.SaveOptionsAsync(settingType, localOptions);
        }
    }

    private static DateTime ToUniversalUtc (DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Utc) return dateTime;
        if (dateTime.Kind == DateTimeKind.Local) return dateTime.ToUniversalTime();
        return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }
}
