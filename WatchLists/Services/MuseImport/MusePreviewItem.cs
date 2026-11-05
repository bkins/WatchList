namespace WatchLists.Services.MuseImport;

public sealed record MusePreviewItem(MuseRecommendation Recommendation
                                   , IReadOnlyList<MuseExistingItem> PossibleMatches);
