namespace WatchLists.Services.ReliableSync;

internal sealed record SyncPending(SyncEnvelope Proposal, SyncSnapshot LocalBefore, string Etag);
