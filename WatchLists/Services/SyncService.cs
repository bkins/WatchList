using WatchLists.Logger;
using WatchLists.Services.ReliableSync;

namespace WatchLists.Services;

public sealed class SyncService
{
#if DEBUG
    private const bool AllowEmulator = true;
#else
    private const bool AllowEmulator = false;
#endif

    private readonly WatchListService _watchList;
    private readonly SettingsService _settings;
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false })
                                       {
                                           Timeout = TimeSpan.FromSeconds(20)
                                       };
    private readonly ISecureStorage _credentials = SecureStorage.Default;
    private bool _publishingChanges;

    public SyncService(WatchListService watchList, SettingsService settings)
    {
        _watchList = watchList;
        _settings  = settings;
        _watchList.WatchListChanged += OnWatchListChanged;
    }

    private void OnWatchListChanged(object? sender, EventArgs eventArgs)
    {
        if (!_publishingChanges)
            _ = Task.Run(SyncAutomaticallyAsync);
    }

    public Task<SyncAttempt> PairAsync(FirebaseSyncProfile profile, string password, CancellationToken cancellationToken)
    {
        return RunAsync(async () =>
        {
            var database = profile.Validate(AllowEmulator);
            var session = await new FirebaseIdentityClient(_http).SignInAsync(profile, database, password, cancellationToken);
            var backup = _watchList.CreateReliableSyncBackup();
            await SaveSessionAsync(profile, database, session);
            await _settings.SaveReliableSyncProfileAsync(profile with { AutoSyncEnabled = false });
            var conflict = GetStore(profile, database, session.UserId).GetConflict();
            return new SyncAttempt($"Signed in. Create new storage or sync to join the existing pairing. Backup: {backup}", conflict);
        }, cancellationToken);
    }

    public Task<SyncAttempt> SyncAsync(bool              create
                                      , SyncResolution? resolution
                                      , CancellationToken cancellationToken)
    {
        return RunAsync(() => SyncPairedAsync(false, create, resolution, cancellationToken), cancellationToken);
    }

    public Task<SyncAttempt> SyncAutomaticallyAsync()
    {
        return RunAsync(() => SyncPairedAsync(true, false, null, CancellationToken.None), CancellationToken.None);
    }

    public Task<SyncAttempt> ReadStatusAsync()
    {
        return RunAsync(async () =>
        {
            var profile = _settings.GetReliableSyncProfile();
            if (profile is null) return new SyncAttempt("Sign in to a private Firebase project to pair upgraded devices.");
            var database = profile.Validate(AllowEmulator);
            var saved = await _credentials.GetAsync(profile.CredentialKey(database));
            if (saved is null) return new SyncAttempt("Sign in again to access this pairing.");
            var account = SyncJson.Read<FirebaseCredential>(saved);
            var conflict = GetStore(profile, database, account.UserId).GetConflict();
            return new SyncAttempt(conflict is null ? "Firebase pairing configured." : "Sync is paused. Review the saved versions.", conflict);
        }, CancellationToken.None);
    }

    public Task<SyncAttempt> SetAutomaticAsync(bool enabled)
    {
        return RunAsync(async () =>
        {
            var profile = _settings.GetReliableSyncProfile();
            if (profile is not null)
                await _settings.SaveReliableSyncProfileAsync(profile with { AutoSyncEnabled = enabled });
            else if (!enabled)
                await _settings.SaveAutoSyncEnabledAsync(false);
            else
                return new SyncAttempt("Pair with Firebase before enabling automatic sync.");
            return new SyncAttempt(enabled ? "Automatic Firebase sync enabled." : "Automatic sync is off.");
        }, CancellationToken.None);
    }

    public Task<SyncAttempt> BackupAsync()
    {
        return RunAsync(() => Task.FromResult(new SyncAttempt("Local sync backup created.", BackupPath: _watchList.CreateReliableSyncBackup()))
                      , CancellationToken.None);
    }

    private async Task<SyncAttempt> SyncPairedAsync(bool                automatic
                                                  , bool                create
                                                  , SyncResolution?     resolution
                                                  , CancellationToken   cancellationToken)
    {
        var profile = _settings.GetReliableSyncProfile();
        if (profile is null) return new SyncAttempt("Sign in and pair with a private Firebase destination first.");
        if (automatic && !profile.AutoSyncEnabled) return new SyncAttempt("Automatic sync is off.");

        var database = profile.Validate(AllowEmulator);
        var saved = await _credentials.GetAsync(profile.CredentialKey(database));
        if (saved is null) return new SyncAttempt("Sign in again to access this pairing.");
        var credential = SyncJson.Read<FirebaseCredential>(saved);
        var store = GetStore(profile, database, credential.UserId);
        if (automatic && store.GetConflict() is { } conflict)
            return new SyncAttempt("Sync is paused. Review the saved versions in Settings.", conflict);

        var session = await new FirebaseIdentityClient(_http).RefreshAsync(profile, database, credential.RefreshToken, cancellationToken);
        if (session.UserId != credential.UserId)
            throw new InvalidDataException("The signed-in Firebase account changed. Sign in again before syncing.");
        await SaveSessionAsync(profile, database, session);

        var remote = new FirebaseReliableSyncRemote(_http, database, profile, session);
        var result = await new ReliableSyncCoordinator(store, remote).SyncAsync(create, resolution, cancellationToken);
        if (result.LocalChanged)
        {
            try
            {
                _publishingChanges = true;
                _watchList.NotifyWatchListChanged();
            }
            finally
            {
                _publishingChanges = false;
            }
        }
        return result;
    }

    private Task<SyncAttempt> RunAsync(Func<Task<SyncAttempt>> operation, CancellationToken cancellationToken)
    {
        return _watchList.SyncAndImportGate.RunAsync(async () =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await operation();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new SyncAttempt("Sync canceled. Any prepared write will be checked on the next attempt.");
            }
            catch (InvalidDataException exception)
            {
                return new SyncAttempt(exception.Message);
            }
            catch (Exception exception)
            {
                await FileLogger.WriteLogAsync($"Reliable sync failed: {exception.GetType().Name}");
                return new SyncAttempt("Firebase sync could not finish. Check the connection and retry.");
            }
        });
    }

    private Task SaveSessionAsync(FirebaseSyncProfile profile, Uri database, FirebaseSession session)
    {
        return _credentials.SetAsync(profile.CredentialKey(database)
                                   , SyncJson.Write(new FirebaseCredential(session.UserId, session.RefreshToken)));
    }

    private ReliableSyncStore GetStore(FirebaseSyncProfile profile, Uri database, string userId)
        => _watchList.CreateReliableSyncStore(profile.DestinationKey(database, userId));
}
