namespace WatchLists.Services.ReliableSync;

internal sealed record SyncDecision(SyncAttempt? Result, SyncPending? Upload, bool LocalChanged = false);
