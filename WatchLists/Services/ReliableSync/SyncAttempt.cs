namespace WatchLists.Services.ReliableSync;

public sealed record SyncAttempt(string        Message
                               , SyncConflict? Conflict     = null
                               , bool          LocalChanged = false
                               , string?       BackupPath   = null);
