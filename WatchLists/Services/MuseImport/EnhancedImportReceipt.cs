namespace WatchLists.Services.MuseImport;

public sealed record EnhancedImportReceipt(int Added, int Enriched, int Unchanged, string? BackupPath);
