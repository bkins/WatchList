namespace WatchLists.Services.MuseImport;

public sealed record MuseRecommendation(string Title
                                      , int? ReleaseYear
                                      , string Description
                                      , string Section
                                      , IReadOnlyList<string> SourceUrls
                                      , int LineNumber);
