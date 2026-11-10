namespace WatchLists.Services.MuseImport;

public sealed record EnhancedRecommendation(string                                           Title
                                          , string                                           YearAndType
                                          , string                                           SourceUrl
                                          , string                                           SourceKey
                                          , IReadOnlyDictionary<RecommendationField, string> Metadata
                                          , string                                           ReviewNote);
