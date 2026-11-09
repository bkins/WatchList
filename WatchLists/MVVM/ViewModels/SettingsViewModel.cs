using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using WatchLists.Logger;
using WatchLists.Services.ReliableSync;
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

    private SyncConflict? _conflict;
    private CancellationTokenSource? _syncCancellation;

    public bool CanEditSync => !IsSyncBusy;
    public bool HasConflict => _conflict is not null;
    public string LocalConflictDetails => Describe(_conflict?.Local);
    public string CloudConflictDetails => Describe(_conflict?.Remote);

    public ObservableCollection<string> StreamingServices { get; } = new();
    public ObservableCollection<string> Categories        { get; } = new();
    public ObservableCollection<string> Types             { get; } = new();

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

        var profile = _settingsService.GetReliableSyncProfile();
        _loadingSyncSettings = true;
        ApiEndpointUrl  = profile?.DatabaseUrl ?? string.Empty;
        SyncCode        = profile?.Code ?? string.Empty;
        FirebaseApiKey  = profile?.ApiKey ?? string.Empty;
        FirebaseEmail   = profile?.Email ?? string.Empty;
        AutoSyncEnabled = _settingsService.GetAutoSyncEnabled();
        _loadingSyncSettings = false;
        if (_syncService is not null) ApplySyncResult(await _syncService.ReadStatusAsync());
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
        if (_syncService is null || IsSyncBusy) return;
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
            var links = snapshot.Links.Count(link => link.WatchItemId == id);
            return $"{item.Title} · {item.Type} · {item.ApiSource} {item.MovieId}\n"
                 + $"Category: {item.Category} · Previous: {item.PreviousCategory}\n"
                 + $"Watched: {item.IsWatched} · Liked: {item.IsLiked} · Deleted: {item.IsDeleted}\n"
                 + $"Service: {item.StreamingService} · {item.DeepLinkUri}\n"
                 + $"Recommendations: {links}";
        });
        return string.Join("\n\n", entries);
    }

    [RelayCommand]
    public async Task NavigateToManageOptions (string optionKey)
    {
        await Shell.Current.GoToAsync($"ManageOptionsPage?optionKey={optionKey}");
    }
}
