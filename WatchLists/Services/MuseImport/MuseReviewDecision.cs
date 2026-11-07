namespace WatchLists.Services.MuseImport;

public sealed record MuseReviewDecision(int                      RowIndex
                                      , MuseReviewDecisionStatus Status
                                      , Guid?                    ItemId     = null
                                      , string                   ItemTitle  = ""
                                      , int                      ProviderId = 0
                                      , string                   Provider   = ""
                                      , string                   MediaType  = ""
                                      , string                   Category   = "");
