namespace WatchLists.Services.MuseImport;

public sealed record WatchItemSyncResult(int InsertedItems
                                       , int UpdatedItems
                                       , int InsertedRecommendations);
