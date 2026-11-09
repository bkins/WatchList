using WatchLists.Services.MuseImport;
using WatchLists.Services.ReliableSync;

namespace WatchList.Import.Tests;

internal sealed class FakeReliableSyncRemote : IReliableSyncRemote
{
    public SyncEnvelope? Current { get; set; }
    public Action? BeforeWrite { get; set; }
    public bool LoseAcknowledgement { get; set; }
    public int WriteCount { get; private set; }
    public int RejectedWrites { get; private set; }

    public Task<SyncRemoteRead> FetchAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SyncRemoteRead(Current, Current?.Revision ?? "null_etag"));
    }

    public Task<CloudSyncWriteResult> WriteAsync(SyncEnvelope envelope, string etag, CancellationToken cancellationToken)
    {
        WriteCount++;
        BeforeWrite?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        if (etag != (Current?.Revision ?? "null_etag"))
        {
            RejectedWrites++;
            return Task.FromResult(CloudSyncWriteResult.Conflict);
        }

        Current = envelope;
        return Task.FromResult(LoseAcknowledgement ? CloudSyncWriteResult.Failed : CloudSyncWriteResult.Success);
    }
}
