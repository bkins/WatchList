namespace WatchLists.Services.Restoration;

public sealed record WatchListRestoreReceipt(WatchListBackupSummary Summary, string RollbackPath);
