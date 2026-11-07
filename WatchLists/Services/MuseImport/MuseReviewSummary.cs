namespace WatchLists.Services.MuseImport;

public sealed record MuseReviewSummary(int TotalRows
                                     , int ApprovedRows
                                     , int NewItems
                                     , int ExistingItemRows
                                     , int SkippedRows
                                     , int PendingRows
                                     , int SourceLinks);
