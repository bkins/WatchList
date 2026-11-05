namespace WatchLists.Services.MuseImport;

public sealed record MuseSourceAssociation(string SourceUrl
                                         , Guid WatchItemId
                                         , string ItemTitle
                                         , bool IsDeleted);
