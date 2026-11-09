namespace WatchLists.Services.ReliableSync;

public sealed class SyncSnapshotMerger
{
    public SyncMergeResult Merge(SyncSnapshot baseline, SyncSnapshot local, SyncSnapshot remote, SyncChoice? choice = null)
    {
        SyncJson.Validate(baseline);
        SyncJson.Validate(local);
        SyncJson.Validate(remote);
        var baseItems   = baseline.Items.ToDictionary(item => item.Id);
        var localItems  = local.Items.ToDictionary(item => item.Id);
        var remoteItems = remote.Items.ToDictionary(item => item.Id);
        var items       = new Dictionary<Guid, SyncItem>();
        var conflicts   = new HashSet<Guid>();

        foreach (var id in baseItems.Keys.Union(localItems.Keys).Union(remoteItems.Keys))
        {
            var original = baseItems.GetValueOrDefault(id);
            var current  = localItems.GetValueOrDefault(id);
            var incoming = remoteItems.GetValueOrDefault(id);
            var item     = WatchItemThreeWayMerge.Merge(original, current, incoming, choice, out var conflict);
            if (conflict) conflicts.Add(id);
            if (item is not null) items.Add(id, item);
        }

        var links = MergeLinks(baseline, local, remote, localItems, remoteItems, items, conflicts, choice);
        ResolveIdentityCollisions(items, localItems, remoteItems, conflicts, choice);
        return new SyncMergeResult(new SyncSnapshot(items.Values.ToArray(), links).Ordered()
                                 , conflicts.Order().ToArray());
    }

    private static SyncLink[] MergeLinks(SyncSnapshot               baseline
                                       , SyncSnapshot               local
                                       , SyncSnapshot               remote
                                       , Dictionary<Guid, SyncItem> localItems
                                       , Dictionary<Guid, SyncItem> remoteItems
                                       , Dictionary<Guid, SyncItem> items
                                       , HashSet<Guid>              conflicts
                                       , SyncChoice?                choice)
    {
        var baseLinks   = baseline.Links.ToDictionary(Key);
        var localLinks  = local.Links.ToDictionary(Key);
        var remoteLinks = remote.Links.ToDictionary(Key);
        var results     = new List<SyncLink>();
        foreach (var key in baseLinks.Keys.Union(localLinks.Keys).Union(remoteLinks.Keys))
        {
            var original = baseLinks.GetValueOrDefault(key);
            var current  = localLinks.GetValueOrDefault(key);
            var incoming = remoteLinks.GetValueOrDefault(key);
            var itemId   = key.WatchItemId;
            if (!items.TryGetValue(itemId, out var target)) continue;

            var localItem = localItems.GetValueOrDefault(itemId);
            var remoteItem = remoteItems.GetValueOrDefault(itemId);
            var localDeleted = localItem?.IsDeleted == true;
            var remoteDeleted = remoteItem?.IsDeleted == true;
            if (localDeleted && !SameLink(incoming, original) && !remoteDeleted
                || remoteDeleted && !SameLink(current, original) && !localDeleted
                || original is not null && (current is null || incoming is null))
            {
                conflicts.Add(itemId);
                if (choice.HasValue)
                {
                    var chosen = WatchItemThreeWayMerge.Choose(localItem, remoteItem, target, choice.Value);
                    if (chosen is not null) items[itemId] = chosen;
                }
            }

            // Links are additive observations. Keep their provenance even if the item becomes a tombstone.
            var combined = UnionLink(original, current, incoming);
            if (combined is not null) results.Add(combined);
        }
        return results.ToArray();
    }

    private static (Guid WatchItemId, string SourceKey) Key(SyncLink link) => (link.WatchItemId, link.SourceKey);

    private static bool SameLink(SyncLink? first, SyncLink? second)
    {
        return first is null || second is null
            ? first == second
            : first.SourceUrl == second.SourceUrl && first.Descriptions.SequenceEqual(second.Descriptions);
    }

    private static SyncLink? UnionLink(SyncLink? original, SyncLink? local, SyncLink? remote)
    {
        var versions = new[] { original, local, remote }.OfType<SyncLink>().ToArray();
        if (versions.Length == 0) return null;
        var chosen = original ?? versions.MinBy(link => link.Id)!;
        return chosen with
               {
                   Descriptions     = versions.SelectMany(link => link.Descriptions).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                 , FirstImportedUtc = versions.Min(link => link.FirstImportedUtc)
                 , LastSeenUtc      = versions.Max(link => link.LastSeenUtc)
               };
    }

    private static void ResolveIdentityCollisions(Dictionary<Guid, SyncItem> items
                                                , Dictionary<Guid, SyncItem> local
                                                , Dictionary<Guid, SyncItem> remote
                                                , HashSet<Guid>              conflicts
                                                , SyncChoice?                choice)
    {
        var preferred = choice == SyncChoice.Local ? local : remote;
        var active = items.Values.Where(item => !item.IsDeleted).ToArray();
        var known = active.Where(HasProviderIdentity)
                          .GroupBy(item => (item.MovieId, Provider: item.ApiSource.Trim().ToUpperInvariant()))
                          .SelectMany(group => group.Any(item => string.IsNullOrWhiteSpace(item.Type))
                              ? [group.ToArray()]
                              : group.GroupBy(item => item.Type.Trim().ToUpperInvariant()).Select(typed => typed.ToArray()));
        var uncertain = active.Where(item => !string.IsNullOrWhiteSpace(item.Title))
                              .GroupBy(item => (Title: item.Title.Trim().ToUpperInvariant(), Type: item.Type.Trim().ToUpperInvariant()))
                              .Where(group => group.Any(item => !HasProviderIdentity(item)))
                              .Select(group => group.ToArray());
        var collisions = known.Concat(uncertain).Where(group => group.Length > 1).ToArray();

        foreach (var group in collisions)
        {
            var remaining = group.Where(item => !items[item.Id].IsDeleted).ToArray();
            if (remaining.Length < 2) continue;
            conflicts.UnionWith(remaining.Select(item => item.Id));
            if (!choice.HasValue) continue;

            var winner = remaining.Where(item => preferred.TryGetValue(item.Id, out var candidate) && !candidate.IsDeleted).ToArray();
            if (winner.Length != 1)
                throw new InvalidDataException("The selected copy still has ambiguous provider identities. Correct the duplicates before retrying.");
            foreach (var item in remaining.Where(item => item.Id != winner[0].Id))
                items[item.Id] = item with { IsDeleted = true };
        }
    }

    private static bool HasProviderIdentity(SyncItem item)
        => item.MovieId > 0 && !string.IsNullOrWhiteSpace(item.ApiSource);
}
