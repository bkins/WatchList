using WatchLists.Services.Models;

namespace WatchLists.Services.MuseImport;

public sealed class CloudSyncUploadCoordinator
{
    public async Task<bool> UploadAsync(SyncBundle initial
                                      , Func<Task<SyncBundle?>> fetch
                                      , Func<SyncBundle, Task> merge
                                      , Func<Task<SyncBundle>> snapshot
                                      , Func<SyncBundle, Task<bool>> upload
                                      , bool allowNewEndpoint)
    {
        var current = initial;
        if (!allowNewEndpoint)
        {
            var remote = await fetch();
            if (remote == null) return false;
            SyncSchemaGuard.Validate(remote.SchemaVersion);
            await merge(remote);
            current = await snapshot();
        }
        return await upload(current);
    }
}
