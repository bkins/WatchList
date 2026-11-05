namespace WatchLists.Services.MuseImport;

public sealed record MuseExistingItem(Guid Id
                                    , string Title
                                    , bool IsDeleted
                                    , int MovieId
                                    , string ApiSource);
