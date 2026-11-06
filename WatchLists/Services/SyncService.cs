using System.Text.Json;
using WatchLists.ExtensionMethods;
using WatchLists.Logger;
using WatchLists.MVVM.Models;
using WatchLists.Services.Enums;
using WatchLists.Services.Interfaces;
using WatchLists.Services.Models;
using WatchLists.Services.MuseImport;

namespace WatchLists.Services;

public class SyncService
{
    private readonly WatchListService    _watchListService;
    private readonly SettingsService     _settingsService;
    private readonly IRemoteSyncProvider _remoteSyncProvider;

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
                await ExportAutomaticAsync();
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

    public Task<string?> CreateNewCloudSyncEndpointAsync()
    {
        return _watchListService.SyncAndImportGate.RunAsync(CreateNewCloudSyncEndpointInternalAsync);
    }

    private async Task<string?> CreateNewCloudSyncEndpointInternalAsync()
    {
        var endpointUrl = "https://watchlist-faa16-default-rtdb.firebaseio.com/";
        var syncCode    = Guid.NewGuid().ToString("N");
        if (!_settingsService.GetSyncMode().EqualsIgnoreCase("CloudApi")) return null;
        if (!await ExportSyncBundleInternalAsync(true, endpointUrl, syncCode)) return null;

        await _settingsService.SaveCloudSyncDestinationAsync(endpointUrl, syncCode);
        return endpointUrl;
    }

    public Task<bool> ExportSyncBundleAsync()
    {
        return _watchListService.SyncAndImportGate.RunAsync(() => ExportSyncBundleInternalAsync());
    }

    public Task<bool> ExportAutomaticAsync()
    {
        return _watchListService.SyncAndImportGate.RunAsync(async () =>
        {
            if (!_settingsService.GetAutoSyncEnabled()) return false;
            return await ExportSyncBundleInternalAsync();
        });
    }

    private async Task<bool> ExportSyncBundleInternalAsync(bool allowNewEndpoint = false, string? endpointOverride = null, string? codeOverride = null)
    {
        try
        {
            var mode = _settingsService.GetSyncMode();

            var bundle = new SyncBundle
                         {
                             SchemaVersion     = SyncSchemaGuard.CurrentVersion
                           , ExportedAtUtc     = DateTime.UtcNow
                           , DeviceId          = DeviceInfo.Name
                           , Items             = _watchListService.GetItemsForImportReview()
                           , Recommendations   = _watchListService.GetRecommendationsForSync()
                           , Categories        = await _settingsService.GetOptionsAsync(SettingType.Categories)
                           , StreamingServices = await _settingsService.GetOptionsAsync(SettingType.StreamingServices)
                           , Types             = await _settingsService.GetOptionsAsync(SettingType.Types)
                           , WatchedCategory   = await _settingsService.GetWatchedCategoryAsync()
                         };

            if (mode.EqualsIgnoreCase("CloudApi"))
            {
                var url      = endpointOverride ?? _settingsService.GetApiEndpointUrl();
                var syncCode = codeOverride ?? _settingsService.GetSyncCode();

                if (url.IsEmptyNullOrWhiteSpace() || syncCode.IsEmptyNullOrWhiteSpace())
                {
                    await FileLogger.WriteLogAsync("ExportSyncBundleAsync CloudApi error: Endpoint URL or Sync Code is empty.");
                    return false;
                }

                var coordinator = new CloudSyncUploadCoordinator();
                var success = await coordinator.UploadAsync(() => _remoteSyncProvider.FetchConditionalAsync(url, syncCode)
                    , async remote => { await MergeBundleIntoLocalDbAsync(remote); }
                    , async () =>
                      {
                          bundle.Items             = _watchListService.GetItemsForImportReview();
                          bundle.Recommendations   = _watchListService.GetRecommendationsForSync();
                          bundle.Categories        = await _settingsService.GetOptionsAsync(SettingType.Categories);
                          bundle.StreamingServices = await _settingsService.GetOptionsAsync(SettingType.StreamingServices);
                          bundle.Types             = await _settingsService.GetOptionsAsync(SettingType.Types);
                          bundle.WatchedCategory   = await _settingsService.GetWatchedCategoryAsync();
                          return bundle;
                      }
                    , (current, etag) => _remoteSyncProvider.UploadConditionalAsync(url, syncCode, current, etag)
                    , allowNewEndpoint);

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

    public Task<string> ImportAndMergeSyncBundleAsync()
    {
        return _watchListService.SyncAndImportGate.RunAsync(ImportAndMergeSyncBundleInternalAsync);
    }

    public Task<string> ImportAutomaticAsync()
    {
        return _watchListService.SyncAndImportGate.RunAsync(async () =>
        {
            if (!_settingsService.GetAutoSyncEnabled()) return "Automatic sync is off.";
            return await ImportAndMergeSyncBundleInternalAsync();
        });
    }

    private async Task<string> ImportAndMergeSyncBundleInternalAsync()
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
                return "Cloud sync could not read the remote payload. No data was uploaded; check the connection or create new sync storage explicitly.";
            }

            try
            {
                SyncSchemaGuard.Validate(bundle.SchemaVersion);
            }
            catch (InvalidDataException exception)
            {
                return exception.Message;
            }

            var (insertedCount, updatedCount, recommendationCount) = await MergeBundleIntoLocalDbAsync(bundle);
            if (insertedCount > 0 || updatedCount > 0 || recommendationCount > 0)
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
        var totalRecommendationCount = 0;
        var processedFilesCount = 0;
        var failedFilesCount = 0;

        foreach (var filePath in files)
        {
            try
            {
                var json   = await File.ReadAllTextAsync(filePath);
                var bundle = JsonSerializer.Deserialize<SyncBundle>(json);

                if (bundle == null) continue;

                SyncSchemaGuard.Validate(bundle.SchemaVersion);

                var (inserted, updated, recommendations) = await MergeBundleIntoLocalDbAsync(bundle);
                totalInsertedCount     += inserted;
                totalUpdatedCount      += updated;
                totalRecommendationCount += recommendations;
                processedFilesCount++;

                if (Path.GetFileName(filePath).ContainsIgnoreCase("conflict"))
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
                failedFilesCount++;
                await FileLogger.WriteLogAsync($"Error importing {filePath}: {ex.Message}");
            }
        }

        if (totalInsertedCount > 0 || totalUpdatedCount > 0 || totalRecommendationCount > 0)
        {
            _watchListService.NotifyWatchListChanged();
        }

        var activeItemsCount = _watchListService.GetWatchItems().Count;
        var resultMsg        = $"Sync complete across {processedFilesCount} files: {totalInsertedCount} inserted, {totalUpdatedCount} updated. Active items: {activeItemsCount}. Files requiring attention: {failedFilesCount}.";
        await FileLogger.WriteLogAsync(resultMsg);
        return resultMsg;
    }

    private async Task<(int Inserted, int Updated, int Recommendations)> MergeBundleIntoLocalDbAsync (SyncBundle bundle)
    {
        var result = _watchListService.MergeSyncItems(bundle.Items ?? []
                                                    , bundle.Recommendations ?? []);

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

        return (result.InsertedItems, result.UpdatedItems, result.InsertedRecommendations);
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

}
