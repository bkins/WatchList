using SQLite;
using WatchLists.Services.MuseImport;

namespace WatchLists.Services.ReliableSync;

public sealed class ReliableSyncCoordinator
{
    private const int MaximumAttempts = 3;
    private readonly ReliableSyncStore _store;
    private readonly IReliableSyncRemote _remote;
    private readonly SyncSnapshotMerger _merger = new();

    public ReliableSyncCoordinator(ReliableSyncStore store, IReliableSyncRemote remote)
    {
        _store  = store;
        _remote = remote;
    }

    public async Task<SyncAttempt> SyncAsync(bool create = false
                                           , SyncResolution? resolution = null
                                           , CancellationToken cancellationToken = default)
    {
        var localChanged = false;
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await _remote.FetchAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(read.Etag))
                throw new InvalidDataException("Firebase returned no revision token.");

            var remote = read.Envelope?.Decode() ?? SyncSnapshot.Empty;
            var decision = _store.Transaction((connection, state) => Prepare(connection, state, read, remote, create, resolution));
            localChanged |= decision.LocalChanged;
            if (decision.Result is { } result)
                return result with { LocalChanged = result.LocalChanged || localChanged };

            var upload = decision.Upload!;
            var status = await _remote.WriteAsync(upload.Proposal, upload.Etag, cancellationToken);
            if (status == CloudSyncWriteResult.Failed)
                return new SyncAttempt("Firebase did not confirm the write. Retry to check its outcome.", LocalChanged: localChanged);

            if (status == CloudSyncWriteResult.Conflict)
            {
                _store.Transaction((connection, state) =>
                {
                    if (state.Pending?.Proposal.Revision == upload.Proposal.Revision)
                        _store.Save(connection, state with { Pending = null });
                    return true;
                });
                continue;
            }

            var completed = _store.Transaction((connection, state) => Acknowledge(connection, state, upload, upload.Proposal.Decode()));
            return completed with { LocalChanged = completed.LocalChanged || localChanged };
        }
        return new SyncAttempt("Another device kept updating Firebase. Try again to sync against its latest copy.", LocalChanged: localChanged);
    }

    private SyncDecision Prepare(SQLiteConnection     connection
                               , SyncDestinationState state
                               , SyncRemoteRead        read
                               , SyncSnapshot          remote
                               , bool                  create
                               , SyncResolution?       resolution)
    {
        var localChanged = false;
        if (state.Pending is { } pending && read.Envelope?.Revision == pending.Proposal.Revision)
        {
            if (read.Envelope != pending.Proposal)
                throw new InvalidDataException("Firebase returned different data for an existing revision.");

            var recovered = Acknowledge(connection, state, pending, remote, read.Etag);
            if (recovered.Conflict is not null) return new SyncDecision(recovered, null);
            state = state with { Baseline = remote, Revision = pending.Proposal.Revision, Pending = null, Conflict = null };
            localChanged = recovered.LocalChanged;
            create = false;
        }

        if (read.Envelope is null && (!create && state.Pending is null || state.Revision is not null))
            return new SyncDecision(new SyncAttempt("The Firebase destination is empty or missing. Use Create Storage for a fresh destination."), null, localChanged);

        if (create && read.Envelope is not null)
            return new SyncDecision(new SyncAttempt("This destination already exists. Use Sync Now to join it."), null, localChanged);

        var local = ReliableSyncStore.ReadLocal(connection);
        var baseline = state.Conflict?.Baseline ?? state.Baseline;
        var current = state.Conflict;
        var resolve = current is not null && resolution?.ConflictId == current.Id
                   && read.Envelope?.Revision == current.RemoteRevision && read.Etag == current.RemoteEtag
                   && SyncJson.Equal(local, current.Local) && SyncJson.Equal(remote, current.Remote);

        var merge = _merger.Merge(baseline, local, remote, resolve ? resolution!.Choice : null);
        if (!resolve && (merge.ConflictingItems.Length > 0 || current is not null))
        {
            var conflict = RecordConflict(connection, state, baseline, local, remote, read, merge.ConflictingItems);
            return new SyncDecision(new SyncAttempt("Sync is paused. Review both versions before choosing a copy.", conflict), null, localChanged);
        }

        if (state.Pending is { } unconfirmed)
            _store.Archive(connection, unconfirmed.Proposal.Revision, unconfirmed);

        if (read.Envelope is not null && SyncJson.Equal(merge.Snapshot, remote))
        {
            var changed = ReliableSyncStore.Apply(connection, local, remote);
            _store.Save(connection, new SyncDestinationState(remote, read.Envelope.Revision, null, null));
            return new SyncDecision(new SyncAttempt("Sync complete.", LocalChanged: changed), null, localChanged);
        }

        SyncJson.Validate(merge.Snapshot);
        var upload = new SyncPending(SyncEnvelope.Create(merge.Snapshot, read.Envelope?.SchemaVersion), local, read.Etag);
        _store.Save(connection, state with { Pending = upload });
        return new SyncDecision(null, upload, localChanged);
    }

    private SyncAttempt Acknowledge(SQLiteConnection      connection
                                   , SyncDestinationState state
                                   , SyncPending          upload
                                   , SyncSnapshot         remote
                                   , string?              readEtag = null)
    {
        if (state.Pending?.Proposal.Revision != upload.Proposal.Revision)
            throw new InvalidOperationException("The prepared Firebase revision changed before acknowledgement.");

        var local = ReliableSyncStore.ReadLocal(connection);
        var merge = SyncJson.Equal(upload.LocalBefore, local)
            ? new SyncMergeResult(remote, [])
            : _merger.Merge(upload.LocalBefore, local, remote);
        var acknowledged = new SyncDestinationState(remote, upload.Proposal.Revision, null, null);

        if (merge.ConflictingItems.Length > 0)
        {
            var read = new SyncRemoteRead(upload.Proposal, readEtag ?? string.Empty);
            var conflict = RecordConflict(connection, acknowledged, upload.LocalBefore, local, remote, read, merge.ConflictingItems);
            return new SyncAttempt("Firebase saved the prepared revision. Review newer local edits before syncing again.", conflict);
        }

        var changed = ReliableSyncStore.Apply(connection, local, merge.Snapshot);
        _store.Save(connection, acknowledged);
        return new SyncAttempt("Sync complete.", LocalChanged: changed);
    }

    private SyncConflict RecordConflict(SQLiteConnection      connection
                                       , SyncDestinationState state
                                       , SyncSnapshot         baseline
                                       , SyncSnapshot         local
                                       , SyncSnapshot         remote
                                       , SyncRemoteRead       read
                                       , Guid[]               items)
    {
        if (state.Conflict is { } existing && existing.RemoteRevision == read.Envelope?.Revision
            && existing.RemoteEtag == read.Etag
            && SyncJson.Equal(existing.Local, local) && SyncJson.Equal(existing.Remote, remote))
            return existing;

        if (state.Pending is { } pending)
            _store.Archive(connection, pending.Proposal.Revision, pending);

        var affected = items.Length > 0 ? items : state.Conflict?.ItemIds ?? [];
        var conflict = new SyncConflict(Guid.NewGuid(), baseline, local, remote, read.Envelope?.Revision, read.Etag, affected);
        _store.Archive(connection, conflict.Id.ToString("N"), conflict);
        _store.Save(connection, state with { Conflict = conflict, Pending = null });
        return conflict;
    }
}
