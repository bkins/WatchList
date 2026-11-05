namespace WatchLists.Services.MuseImport;

public sealed record MuseParseResult(IReadOnlyList<MuseRecommendation> Recommendations
                                   , IReadOnlyList<string> Warnings);
