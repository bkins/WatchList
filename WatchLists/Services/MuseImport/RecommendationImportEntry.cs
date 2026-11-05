namespace WatchLists.Services.MuseImport;

public sealed record RecommendationImportEntry(Guid WatchItemId
                                             , string SourceUrl
                                             , string Description);
