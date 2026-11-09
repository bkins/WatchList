namespace WatchLists.Services.ReliableSync;

public sealed record SyncResolution(Guid ConflictId, SyncChoice Choice);
