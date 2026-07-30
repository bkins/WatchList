using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchLists.ExtensionMethods;
using WatchLists.Services;
using WatchLists.Services.Enums;

namespace WatchLists.MVVM.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly SyncService?    _syncService;

    [ObservableProperty] private string _selectedStreamingService = string.Empty;
    [ObservableProperty] private string _selectedCategory         = string.Empty;
    [ObservableProperty] private string _selectedType             = string.Empty;

    [ObservableProperty] private string _syncFolderPath    = string.Empty;
    [ObservableProperty] private bool   _autoSyncEnabled;
    [ObservableProperty] private string _syncStatusMessage = string.Empty;
    [ObservableProperty] private string _syncMode          = "CloudApi";
    [ObservableProperty] private string _apiEndpointUrl    = string.Empty;
    [ObservableProperty] private string _syncCode          = string.Empty;

    public bool IsCloudApiMode => SyncMode.EqualsIgnoreCase("CloudApi");
    public bool IsFileSystemMode => !IsCloudApiMode;

    public ObservableCollection<string> StreamingServices { get; } = new();
    public ObservableCollection<string> Categories        { get; } = new();
    public ObservableCollection<string> Types             { get; } = new();
    public ObservableCollection<string> SyncModes          { get; } = new() { "CloudApi", "FileSystem" };

    private string _selectedWatchedCategory = string.Empty;

    public string SelectedWatchedCategory
    {
        get => _selectedWatchedCategory;
        set
        {
            if (_selectedWatchedCategory == value) return;

            _selectedWatchedCategory = value;
            OnPropertyChanged();

            if (value.HasValue())
            {
                _ = _settingsService.SaveWatchedCategoryAsync(value);
            }
        }
    }

    public SettingsViewModel (SettingsService settingsService
                            , SyncService?    syncService = null)
    {
        _settingsService = settingsService;
        _syncService     = syncService;
    }

    public async Task LoadSettingsAsync()
    {
        var streaming  = await _settingsService.GetOptionsAsync(SettingType.StreamingServices);
        var categories = await _settingsService.GetOptionsAsync(SettingType.Categories);
        var types      = await _settingsService.GetOptionsAsync(SettingType.Types);

        StreamingServices.Clear();
        foreach (var item in streaming)
        {
            StreamingServices.Add(item);
        }

        Categories.Clear();
        foreach (var item in categories)
        {
            Categories.Add(item);
        }

        Types.Clear();
        foreach (var item in types)
        {
            Types.Add(item);
        }

        SelectedWatchedCategory = await _settingsService.GetWatchedCategoryAsync();

        SyncMode        = _settingsService.GetSyncMode();
        ApiEndpointUrl  = _settingsService.GetApiEndpointUrl();
        SyncCode        = _settingsService.GetSyncCode();
        SyncFolderPath  = _settingsService.GetSyncFolderPath();
        AutoSyncEnabled = _settingsService.GetAutoSyncEnabled();
    }

    partial void OnSyncModeChanged (string value)
    {
        _ = _settingsService.SaveSyncModeAsync(value);
        OnPropertyChanged(nameof(IsCloudApiMode));
        OnPropertyChanged(nameof(IsFileSystemMode));
    }

    partial void OnApiEndpointUrlChanged (string value)
    {
        _ = _settingsService.SaveApiEndpointUrlAsync(value);
    }

    partial void OnSyncCodeChanged (string value)
    {
        _ = _settingsService.SaveSyncCodeAsync(value);
    }

    partial void OnSyncFolderPathChanged (string value)
    {
        _ = _settingsService.SaveSyncFolderPathAsync(value);
    }

    [RelayCommand]
    public async Task CreateNewCloudEndpoint()
    {
        if (_syncService == null)
        {
            SyncStatusMessage = "Sync service unavailable.";
            return;
        }

        SyncStatusMessage = "Creating new cloud sync storage...";
        var newUrl = await _syncService.CreateNewCloudSyncEndpointAsync();

        if (newUrl.HasValue())
        {
            ApiEndpointUrl    = newUrl;
            SyncStatusMessage = $"Created cloud storage! Copy this Endpoint URL to your phone to sync: {newUrl}";
        }
        else
        {
            SyncStatusMessage = "Failed to create cloud storage. Please check internet connection.";
        }
    }

    [RelayCommand]
    public async Task PickSyncFolder()
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
            if (result.IsSuccessful && result.Folder != null)
            {
                var folderPath = result.Folder.Path;
                if (folderPath.IsEmptyNullOrWhiteSpace())
                {
                    folderPath = result.Folder.Name;
                }

                if (folderPath.HasValue())
                {
                    SyncFolderPath    = folderPath;
                    SyncStatusMessage = $"Selected sync directory: {SyncFolderPath}";
                }
                else
                {
                    SyncStatusMessage = "Folder selected, but path could not be resolved. Please enter path manually.";
                }
            }
            else if (result.Exception != null)
            {
                SyncStatusMessage = $"Folder picker error: {result.Exception.Message}";
            }
        }
        catch (Exception ex)
        {
            SyncStatusMessage = $"Folder selection error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void UseDefaultAppFolder()
    {
        SyncFolderPath    = FileSystem.Current.AppDataDirectory;
        SyncStatusMessage = $"Set to default app folder: {SyncFolderPath}";
    }

    [RelayCommand]
    public async Task SyncNow()
    {
        if (_syncService == null)
        {
            SyncStatusMessage = "Sync service unavailable.";
            return;
        }

#if ANDROID
        try
        {
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
            {
                if (!Android.OS.Environment.IsExternalStorageManager)
                {
                    var uri    = Android.Net.Uri.Parse("package:" + Android.App.Application.Context.PackageName);
                    var intent = new Android.Content.Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission, uri);
                    intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                    Android.App.Application.Context.StartActivity(intent);
                    SyncStatusMessage = "Please grant 'All Files Access' permission in Android Settings, then tap Sync Now again.";
                    return;
                }
            }
            else
            {
                var status = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                if (status != PermissionStatus.Granted)
                {
                    await Permissions.RequestAsync<Permissions.StorageWrite>();
                }
            }
        }
        catch
        {
            // Fallback for Android versions where StorageWrite is not applicable
        }
#endif

        SyncStatusMessage = "Syncing data...";
        var importResult  = await _syncService.ImportAndMergeSyncBundleAsync();
        var exportResult  = await _syncService.ExportSyncBundleAsync();

        if (exportResult)
        {
            SyncStatusMessage = importResult;
        }
        else
        {
            if (importResult.Contains("Sync complete") || importResult.Contains("initialized") || importResult.Contains("created"))
            {
                SyncStatusMessage = $"{importResult} (Cloud Upload Pending)";
            }
            else
            {
                SyncStatusMessage = $"{importResult} (Export: Failed)";
            }
        }
        await LoadSettingsAsync();
    }

    [RelayCommand]
    public async Task NavigateToManageOptions (string optionKey)
    {
        await Shell.Current.GoToAsync($"ManageOptionsPage?optionKey={optionKey}");
    }
}
