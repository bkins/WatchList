using WatchLists.MVVM.Models;

namespace WatchLists.Services.MuseImport;

public sealed class RecommendationSyncMapper
{
    public Dictionary<Guid, Guid> Map(IEnumerable<WatchItem> incomingItems
                                    , IEnumerable<WatchItem> localItems
                                    , IEnumerable<MovieRecommendation> recommendations)
    {
        ArgumentNullException.ThrowIfNull(incomingItems);
        ArgumentNullException.ThrowIfNull(localItems);
        ArgumentNullException.ThrowIfNull(recommendations);

        var incoming = incomingItems.ToList();
        var local    = localItems.ToList();
        var mapping  = new Dictionary<Guid, Guid>();

        foreach (var incomingId in recommendations.Select(item => item.WatchItemId).Distinct())
        {
            var sourceItems = incoming.Where(item => item.Id == incomingId).ToList();
            if (incomingId == Guid.Empty || sourceItems.Count != 1)
            {
                throw new InvalidOperationException("Recommendation sync requires exactly one matching source item.");
            }

            var source = sourceItems[0];
            var exact = local.Where(item => item.Id == incomingId).ToList();
            if (exact.Count > 1) throw new InvalidOperationException("Duplicate local item IDs prevent recommendation sync.");
            if (exact.Count == 1)
            {
                mapping.Add(incomingId, exact[0].Id);
                continue;
            }

            var candidates = source.MovieId > 0 && !string.IsNullOrWhiteSpace(source.ApiSource)
                ? local.Where(item => item.MovieId == source.MovieId &&
                                      string.Equals(item.ApiSource, source.ApiSource, StringComparison.OrdinalIgnoreCase))
                       .ToList()
                : [];
            if (candidates.Count != 1)
            {
                throw new InvalidOperationException("Recommendation sync could not uniquely identify the local item.");
            }
            mapping.Add(incomingId, candidates[0].Id);
        }

        return mapping;
    }
}
