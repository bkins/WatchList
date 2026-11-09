namespace WatchLists.Services.ReliableSync;

public sealed record SyncMergeResult(SyncSnapshot Snapshot, Guid[] ConflictingItems);
