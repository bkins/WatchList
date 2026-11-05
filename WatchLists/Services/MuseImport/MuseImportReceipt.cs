namespace WatchLists.Services.MuseImport;

public sealed record MuseImportReceipt(int NewItems
                                     , int NewRecommendations
                                     , string BackupPath);
