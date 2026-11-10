using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using WatchLists.Logger;
using WatchLists.Services.ReliableSync;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchLists.ExtensionMethods;
using WatchLists.Services;
using WatchLists.Services.Restoration;
using WatchLists.Services.Enums;
using WatchLists.Services.MuseImport;
using WatchLists.MVVM.Views;

namespace WatchLists.MVVM.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly SyncService?    _syncService;
    private readonly WatchListService? _watchListService;
    private bool _loadingSyncSettings;

    [ObservableProperty] public partial string SelectedStreamingService { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedCategory { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedType { get; set; } = string.Empty;

    [ObservableProperty] public partial bool AutoSyncEnabled { get; set; }
    [ObservableProperty] public partial string SyncStatusMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial string ApiEndpointUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial string SyncCode { get; set; } = string.Empty;
    [ObservableProperty] public partial string FirebaseApiKey { get; set; } = string.Empty;
    [ObservableProperty] public partial string FirebaseEmail { get; set; } = string.Empty;
    [ObservableProperty] public partial string FirebasePassword { get; set; } = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSync))]
    public partial bool IsSyncBusy { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSync))]
    public partial bool IsRestoreBusy { get; set; }
    [ObservableProperty] public partial string RestoreStatus { get; set; } = string.Empty;
    [ObservableProperty] public partial WatchListBackupFile? SelectedRestoreBackup { get; set; }

    private SyncConflict? _conflict;
    private CancellationTokenSource? _syncCancellation;

    public bool CanEditSync => !IsSyncBusy && !IsRestoreBusy;
    public bool HasConflict => _conflict is not null;
    public string LocalConflictDetails => Describe(_conflict?.Local);
    public string CloudConflictDetails => Describe(_conflict?.Remote);

    public ObservableCollection<string> StreamingServices { get; } = new();
    public ObservableCollection<string> Categories        { get; } = new();
    public ObservableCollection<string> Types             { get; } = new();
    public ObservableCollection<WatchListBackupFile> RestoreBackups { get; } = new();

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

    public SettingsViewModel(SettingsService    settingsService
                            , SyncService?       syncService      = null
                            , WatchListService?  watchListService = null)
    {
        _settingsService = settingsService;
        _syncService     = syncService;
        _watchListService = watchListService;
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

        var profile = _settingsService.GetReliableSyncProfile();
        _loadingSyncSettings = true;
        ApiEndpointUrl  = profile?.DatabaseUrl ?? string.Empty;
        SyncCode        = profile?.Code ?? string.Empty;
        FirebaseApiKey  = profile?.ApiKey ?? string.Empty;
        FirebaseEmail   = profile?.Email ?? string.Empty;
        AutoSyncEnabled = _settingsService.GetAutoSyncEnabled();
        _loadingSyncSettings = false;
        if (_syncService is not null) ApplySyncResult(await _syncService.ReadStatusAsync());
        RefreshRestoreBackups();
    }

    private void RefreshRestoreBackups()
    {
        if (_watchListService is null) return;
        var selected = SelectedRestoreBackup;
        RestoreBackups.Clear();
        foreach (var backup in _watchListService.GetLocalBackups())
            RestoreBackups.Add(backup);
        SelectedRestoreBackup = RestoreBackups.FirstOrDefault(backup => backup == selected);
    }

    [RelayCommand]
    private Task ReviewLocalRestoreBackup()
    {
        if (SelectedRestoreBackup is not { } backup)
        {
            RestoreStatus = "Choose a local backup first.";
            return Task.CompletedTask;
        }

        return RestoreFromAsync(() => Task.FromResult<(string, Stream)?>((
            Path.GetFileName(backup.Path), File.OpenRead(backup.Path))));
    }

    [RelayCommand]
    private Task PickRestoreBackup()
    {
        return RestoreFromAsync(async () =>
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose a WatchList backup" });
            return file is null ? null : (file.FileName, await file.OpenReadAsync());
        });
    }

    private async Task RestoreFromAsync(Func<Task<(string Name, Stream Content)?>> select)
    {
        if (!CanEditSync || _watchListService is null) return;
        if (_settingsService.GetAutoSyncEnabled())
        {
            RestoreStatus = "Turn off automatic sync before restoring a backup.";
            return;
        }

        IsRestoreBusy = true;
        try
        {
            var selection = await select();
            if (selection is null)
            {
                RestoreStatus = "Backup selection canceled.";
                return;
            }

            RestoreStatus = "Validating the selected backup…";
            PreparedWatchListBackup prepared;
            await using (var content = selection.Value.Content)
                prepared = await _watchListService.PrepareBackupAsync(content);
            using (prepared)
            {
                var summary = prepared.Summary;
                var syncInformation = summary.HasSyncState ? "Includes saved sync recovery state." : "Includes local WatchList data.";
                var confirmed = await Shell.Current.DisplayAlertAsync("Restore WatchList backup?"
                    , $"{selection.Value.Name}\n{summary.Items} active items, {summary.DeletedItems} deleted items, "
                      + $"{summary.Recommendations} recommendation links. {syncInformation}\n\n"
                      + "This replaces the local list. WatchList first saves a rollback backup. Settings are stored separately."
                    , "Restore", "Cancel");
                if (!confirmed)
                {
                    RestoreStatus = "Restore canceled.";
                    return;
                }

                RestoreStatus = "Restoring and reopening the WatchList…";
                var receipt = await _watchListService.RestoreBackupAsync(prepared);
                RestoreStatus = $"Restored {receipt.Summary.Items} active items and {receipt.Summary.Recommendations} recommendation links. "
                              + $"Rollback backup saved in RestoreBackups as {Path.GetFileName(receipt.RollbackPath)}.";
                RefreshRestoreBackups();
                if (_syncService is not null)
                {
                    var syncStatus = await _syncService.ReadStatusAsync();
                    ApplySyncResult(syncStatus);
                    SetConflict(syncStatus.Conflict);
                }
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or IOException)
        {
            RestoreStatus = exception.Message;
        }
        catch (Exception exception)
        {
            await FileLogger.WriteLogAsync($"Backup restore failed: {exception.GetType().Name}");
            RestoreStatus = "The backup could not be opened or restored.";
        }
        finally
        {
            IsRestoreBusy = false;
        }
    }

    partial void OnAutoSyncEnabledChanged(bool value)
    {
        if (!_loadingSyncSettings && _syncService is not null)
            _ = SaveAutomaticAsync(value);
    }

    private async Task SaveAutomaticAsync(bool enabled)
    {
        var result = await _syncService!.SetAutomaticAsync(enabled);
        _loadingSyncSettings = true;
        AutoSyncEnabled = _settingsService.GetAutoSyncEnabled();
        _loadingSyncSettings = false;
        SyncStatusMessage = result.Message;
    }

    [RelayCommand]
    private Task PairFirebase()
    {
        var code = string.IsNullOrWhiteSpace(SyncCode) ? Guid.NewGuid().ToString("N") : SyncCode.Trim();
        var profile = new FirebaseSyncProfile(ApiEndpointUrl.Trim(), FirebaseApiKey.Trim(), FirebaseEmail.Trim(), code, false);
        var password = FirebasePassword;
        FirebasePassword = string.Empty;
        return RunSyncAsync(async (service, cancellationToken) =>
        {
            var result = await service.PairAsync(profile, password, cancellationToken);
            var saved  = _settingsService.GetReliableSyncProfile();
            if (saved == profile)
            {
                SyncCode = code;
                _loadingSyncSettings = true;
                AutoSyncEnabled = false;
                _loadingSyncSettings = false;
                SetConflict(result.Conflict);
            }
            return result;
        });
    }

    [RelayCommand]
    private Task CreateNewCloudEndpoint()
        => RunSyncAsync((service, cancellationToken) => RunPairedAsync(service, true, null, cancellationToken));

    [RelayCommand]
    private Task SyncNow()
        => RunSyncAsync((service, cancellationToken) => RunPairedAsync(service, false, null, cancellationToken));

    [RelayCommand]
    private Task KeepLocal()
        => ResolveAsync(SyncChoice.Local);

    [RelayCommand]
    private Task KeepCloud()
        => ResolveAsync(SyncChoice.Cloud);

    [RelayCommand]
    private Task BackupSyncData()
    {
        return RunSyncAsync(async (service, cancellationToken) =>
        {
            var backup = await service.BackupAsync();
            if (backup.BackupPath is not { } path) return backup;
            await using var file = File.OpenRead(path);
            var exported = await FileSaver.Default.SaveAsync(Path.GetFileName(path), file, cancellationToken);
            return new SyncAttempt(exported.IsSuccessful
                ? "Local sync backup saved to the selected location."
                : "Local backup created. Export did not complete.");
        });
    }

    [RelayCommand]
    private void CancelSync() => _syncCancellation?.Cancel();

    private Task ResolveAsync(SyncChoice choice)
    {
        if (_conflict is not { } conflict) return Task.CompletedTask;
        return RunSyncAsync((service, cancellationToken) =>
            RunPairedAsync(service, false, new SyncResolution(conflict.Id, choice), cancellationToken));
    }

    private Task<SyncAttempt> RunPairedAsync(SyncService       service
                                            , bool              create
                                            , SyncResolution?    resolution
                                            , CancellationToken cancellationToken)
    {
        var saved = _settingsService.GetReliableSyncProfile();
        if (saved is null || saved.DatabaseUrl != ApiEndpointUrl.Trim() || saved.Code != SyncCode.Trim()
            || saved.ApiKey != FirebaseApiKey.Trim() || saved.Email != FirebaseEmail.Trim())
            return Task.FromResult(new SyncAttempt("Sign in to save these pairing details before syncing."));

        return service.SyncAsync(create, resolution, cancellationToken);
    }

    private async Task RunSyncAsync(Func<SyncService, CancellationToken, Task<SyncAttempt>> action)
    {
        if (_syncService is null || !CanEditSync) return;
        using var cancellation = new CancellationTokenSource();
        _syncCancellation = cancellation;
        IsSyncBusy = true;
        SyncStatusMessage = "Working with Firebase…";
        try
        {
            ApplySyncResult(await action(_syncService, cancellation.Token));
        }
        catch (Exception exception)
        {
            await FileLogger.WriteLogAsync($"Sync setup failed: {exception.GetType().Name}");
            SyncStatusMessage = "Could not load or save the Firebase settings.";
        }
        finally
        {
            _syncCancellation = null;
            IsSyncBusy = false;
        }
    }

    private void ApplySyncResult(SyncAttempt result)
    {
        SyncStatusMessage = result.Message;
        if (result.Conflict is not null || result.Message is "Sync complete." or "Firebase pairing configured.")
            SetConflict(result.Conflict);
    }

    private void SetConflict(SyncConflict? conflict)
    {
        _conflict = conflict;
        OnPropertyChanged(nameof(HasConflict));
        OnPropertyChanged(nameof(LocalConflictDetails));
        OnPropertyChanged(nameof(CloudConflictDetails));
    }

    private string Describe(SyncSnapshot? snapshot)
    {
        if (snapshot is null || _conflict is null) return string.Empty;
        var entries = _conflict.ItemIds.Select(id =>
        {
            var item = snapshot.Items.FirstOrDefault(candidate => candidate.Id == id);
            if (item is null) return $"{id}: missing from this copy";
            var links = snapshot.Links.Where(link => link.WatchItemId == id).ToArray();
            var active = links.Where(link => link.IsRemoved != true).Select(link => link.ToRecommendation()).ToArray();
            var recommendations = $"Recommendations: {active.Length}";
            if (links.Any(link => link.IsEnhanced))
            {
                var primary = RecommendationSet.Primary(active)?.Id;
                var descriptions = active.Select(row => DescribeRecommendation(row, row.Id == primary));
                recommendations += $" · Removed: {links.Length - active.Length}\n" + string.Join("\n\n", descriptions);
            }
            return $"{item.Title} · {item.Type} · {item.ApiSource} {item.MovieId}\n"
                 + $"Category: {item.Category} · Previous: {item.PreviousCategory}\n"
                 + $"Watched: {item.IsWatched} · Liked: {item.IsLiked} · Deleted: {item.IsDeleted}\n"
                 + $"Service: {item.StreamingService} · {item.DeepLinkUri}\n"
                 + recommendations;
        });
        return string.Join("\n\n", entries);
    }

    private static string DescribeRecommendation(MovieRecommendation recommendation, bool primary)
    {
        var heading = RecommendationFields.Name(recommendation) + (primary ? " · Primary" : string.Empty);
        if (RecommendationMetadata.HasSpoilers(recommendation)) return heading + "\nSpoiler content hidden";
        var values = RecommendationMetadata.Effective(recommendation);
        var lines = values.Where(field => field.Key != RecommendationField.ContainsSpoilers && !string.IsNullOrWhiteSpace(field.Value))
                          .OrderBy(field => field.Key)
                          .Select(field => $"{RecommendationFields.Label(field.Key)}: {field.Value}");
        return string.Join("\n", new[] { heading }.Concat(lines));
    }

    [RelayCommand]
    public async Task NavigateToManageOptions (string optionKey)
    {
        await Shell.Current.GoToAsync($"ManageOptionsPage?optionKey={optionKey}");
    }
}
