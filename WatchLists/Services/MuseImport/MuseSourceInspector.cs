using WatchLists.MVVM.Models;

namespace WatchLists.Services.MuseImport;

public sealed class MuseSourceInspector
{
    public IReadOnlyList<MuseSourceAssociation> Find(MuseRecommendation row
                                                   , IEnumerable<MovieRecommendation> saved
                                                   , IEnumerable<WatchItem> watchItems)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(watchItems);

        var items = watchItems.ToDictionary(item => item.Id);
        var sourceKeys = row.SourceUrls.Select(RecommendationSourceKey.Create).ToHashSet(StringComparer.Ordinal);
        return saved.Where(recommendation => sourceKeys.Contains(recommendation.SourceKey))
                    .Select(recommendation =>
                    {
                        if (!items.TryGetValue(recommendation.WatchItemId, out var item))
                        {
                            throw new InvalidOperationException("A saved recommendation refers to a missing watch item.");
                        }

                        return new MuseSourceAssociation(recommendation.SourceUrl, item.Id, item.Title, item.IsDeleted);
                    })
                    .ToList();
    }
}
