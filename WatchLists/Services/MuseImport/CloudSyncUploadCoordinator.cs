using WatchLists.Services.Models;

namespace WatchLists.Services.MuseImport;

public sealed class CloudSyncUploadCoordinator
{
    private const int MaximumAttempts = 3;

    public async Task<bool> UploadAsync(Func<Task<CloudSyncReadResult?>> fetch
                                      , Func<SyncBundle, Task> merge
                                      , Func<Task<SyncBundle>> snapshot
                                      , Func<SyncBundle, string, Task<CloudSyncWriteResult>> upload
                                      , bool allowEmptyDestination)
    {
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var remote = await fetch();
            if (remote == null || string.IsNullOrWhiteSpace(remote.Etag)) return false;

            if (remote.Bundle == null)
            {
                if (!allowEmptyDestination) return false;
            }
            else
            {
                if (allowEmptyDestination) return false;
                SyncSchemaGuard.Validate(remote.Bundle.SchemaVersion);
                await merge(remote.Bundle);
            }

            var current = await snapshot();
            var result  = await upload(current, remote.Etag);
            if (result == CloudSyncWriteResult.Success) return true;
            if (result != CloudSyncWriteResult.Conflict) return false;
        }

        return false;
    }
}
