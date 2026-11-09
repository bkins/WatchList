using WatchLists.Services.MuseImport;

namespace WatchLists.Services.ReliableSync;

public interface IReliableSyncRemote
{
    Task<SyncRemoteRead> FetchAsync(CancellationToken cancellationToken);
    Task<CloudSyncWriteResult> WriteAsync(SyncEnvelope envelope, string etag, CancellationToken cancellationToken);
}
