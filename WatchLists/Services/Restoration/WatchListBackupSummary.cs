namespace WatchLists.Services.Restoration;

public sealed record WatchListBackupSummary(int Items, int DeletedItems, int Recommendations, bool HasSyncState);
