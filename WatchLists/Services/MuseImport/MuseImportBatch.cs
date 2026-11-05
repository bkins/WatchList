using WatchLists.MVVM.Models;

namespace WatchLists.Services.MuseImport;

public sealed record MuseImportBatch(IReadOnlyList<WatchItem> NewItems
                                   , IReadOnlyList<RecommendationImportEntry> Recommendations);
