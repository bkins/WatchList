namespace WatchLists.Services.MuseImport;

public sealed class MusePreviewBuilder
{
    public IReadOnlyList<MusePreviewItem> Build(IEnumerable<MuseRecommendation> recommendations
                                              , IEnumerable<MuseExistingItem> existingItems)
    {
        ArgumentNullException.ThrowIfNull(recommendations);
        ArgumentNullException.ThrowIfNull(existingItems);

        var items = existingItems.ToList();

        return recommendations.Select(recommendation =>
                                   {
                                       var candidates = items.Where(item => string.Equals(item.Title.Trim()
                                                                                       , recommendation.Title.Trim()
                                                                                       , StringComparison.OrdinalIgnoreCase))
                                                             .ToList();
                                       return new MusePreviewItem(recommendation, candidates);
                                   })
                              .ToList();
    }
}
