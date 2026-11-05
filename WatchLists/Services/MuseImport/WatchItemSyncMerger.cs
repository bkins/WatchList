using SQLite;
using WatchLists.MVVM.Models;

namespace WatchLists.Services.MuseImport;

public sealed class WatchItemSyncMerger
{
    private readonly SQLiteConnection _connection;

    public WatchItemSyncMerger(SQLiteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public WatchItemSyncResult Merge(IEnumerable<WatchItem> incomingItems
                                   , IEnumerable<MovieRecommendation> incomingRecommendations)
    {
        ArgumentNullException.ThrowIfNull(incomingItems);
        ArgumentNullException.ThrowIfNull(incomingRecommendations);

        var incoming        = incomingItems.ToList();
        var recommendations = incomingRecommendations.ToList();
        var referencedIds   = recommendations.Select(item => item.WatchItemId).ToHashSet();
        var inserted        = 0;
        var updated         = 0;
        var sources         = 0;

        _connection.RunInTransaction(() =>
        {
            var localItems = _connection.Table<WatchItem>().ToList();
            foreach (var sourceItem in incoming)
            {
                var originalId = sourceItem.Id;
                var strict     = referencedIds.Contains(originalId);
                var match      = FindMatch(sourceItem, localItems, strict);
                if (match == null)
                {
                    _connection.Insert(sourceItem);
                    localItems.Add(sourceItem);
                    inserted++;
                    continue;
                }

                if (strict && match.IsDeleted && !sourceItem.IsDeleted) continue;
                if (ToUtc(sourceItem.LastUpdated) <= ToUtc(match.LastUpdated)) continue;

                try
                {
                    sourceItem.Id = match.Id;
                    _connection.Update(sourceItem);
                    var index = localItems.IndexOf(match);
                    localItems[index] = _connection.Find<WatchItem>(match.Id);
                    updated++;
                }
                finally
                {
                    sourceItem.Id = originalId;
                }
            }

            if (recommendations.Count > 0)
            {
                var mapping = new RecommendationSyncMapper().Map(incoming, localItems, recommendations);
                sources = new RecommendationStore(_connection).MergeFromSync(recommendations, mapping);
            }
        });

        return new WatchItemSyncResult(inserted, updated, sources);
    }

    private static WatchItem? FindMatch(WatchItem incoming, List<WatchItem> localItems, bool strict)
    {
        var idMatches = localItems.Where(item => item.Id == incoming.Id).ToList();
        if (idMatches.Count == 1) return idMatches[0];
        if (idMatches.Count > 1) throw new InvalidOperationException("Duplicate watch item IDs prevent sync.");

        if (incoming.MovieId > 0)
        {
            var matches = localItems.Where(item => item.MovieId == incoming.MovieId &&
                                                   ProviderMatches(item.ApiSource, incoming.ApiSource, strict))
                                    .ToList();
            if (strict && matches.Count > 1) throw new InvalidOperationException("Ambiguous provider identities prevent recommendation sync.");
            if (matches.Count > 0) return matches[0];
        }

        if (strict || string.IsNullOrWhiteSpace(incoming.Title)) return null;
        return localItems.FirstOrDefault(item => string.Equals(item.Title, incoming.Title, StringComparison.OrdinalIgnoreCase) &&
                                                 (string.IsNullOrWhiteSpace(item.Type) || string.IsNullOrWhiteSpace(incoming.Type) ||
                                                  string.Equals(item.Type, incoming.Type, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool ProviderMatches(string local, string incoming, bool strict)
    {
        if (strict && (string.IsNullOrWhiteSpace(local) || string.IsNullOrWhiteSpace(incoming))) return false;
        return string.IsNullOrWhiteSpace(local) || string.IsNullOrWhiteSpace(incoming) ||
               string.Equals(local, incoming, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
