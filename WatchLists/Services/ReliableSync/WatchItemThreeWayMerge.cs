namespace WatchLists.Services.ReliableSync;

internal static class WatchItemThreeWayMerge
{
    public static bool Same(SyncItem? first, SyncItem? second)
    {
        if (first is null || second is null) return first == second;
        return first with { LastUpdated = default } == second with { LastUpdated = default };
    }

    public static SyncItem? Merge(SyncItem? baseline
                                , SyncItem? local
                                , SyncItem? remote
                                , SyncChoice? choice
                                , out bool conflict)
    {
        conflict = false;
        if (baseline is not null && local is null && remote is null)
        {
            conflict = true;
            return baseline with { IsDeleted = true };
        }
        if (Same(local, remote))
            return local is not null && remote!.LastUpdated > local.LastUpdated ? remote : local;
        if (baseline is null && (local is null || remote is null)) return local ?? remote;

        // Existing items must travel with a tombstone. Absence is not proof of deletion.
        if (baseline is null || local is null || remote is null)
        {
            conflict = true;
            return choice.HasValue ? Choose(local, remote, baseline, choice.Value) : local ?? remote;
        }

        if (Same(local, baseline)) return remote;
        if (Same(remote, baseline)) return local;

        if (local.IsDeleted != baseline.IsDeleted || remote.IsDeleted != baseline.IsDeleted || baseline.IsDeleted)
        {
            conflict = true;
            return choice == SyncChoice.Cloud ? remote : local;
        }

        var watch     = Pick(Watch(baseline), Watch(local), Watch(remote), choice, ref conflict);
        var streaming = Pick(Streaming(baseline), Streaming(local), Streaming(remote), choice, ref conflict);
        var identity  = Pick(Identity(baseline), Identity(local), Identity(remote), choice, ref conflict);
        var liked     = Pick(baseline.IsLiked, local.IsLiked, remote.IsLiked, choice, ref conflict);

        if ((IdentityKey(local) != IdentityKey(baseline) && Streaming(remote) != Streaming(baseline)
             || IdentityKey(remote) != IdentityKey(baseline) && Streaming(local) != Streaming(baseline))
            && (IdentityKey(local) != IdentityKey(remote) || Streaming(local) != Streaming(remote)))
        {
            conflict  = true;
            var owner = choice == SyncChoice.Cloud ? remote : local;
            identity  = Identity(owner);
            streaming = Streaming(owner);
        }

        return local with
               {
                   Category                   = watch.Category
                 , PreviousCategory           = watch.PreviousCategory
                 , IsWatched                  = watch.IsWatched
                 , IsLiked                    = liked
                 , StreamingService           = streaming.StreamingService
                 , DeepLinkUri                = streaming.DeepLinkUri
                 , MovieId                    = identity.MovieId
                 , ApiSource                  = identity.ApiSource
                 , Type                       = identity.Type
                 , Title                      = identity.Title
                 , Overview                   = identity.Overview
                 , PosterUrl                  = identity.PosterUrl
                 , AvailableStreamingServices = identity.AvailableStreamingServices
                 , AggregatedDataJson         = identity.AggregatedDataJson
                 , LastUpdated                = local.LastUpdated > remote.LastUpdated ? local.LastUpdated : remote.LastUpdated
               };
    }

    public static SyncItem? Choose(SyncItem? baselineLocal, SyncItem? remote, SyncItem? baseline, SyncChoice choice)
    {
        var preferred = choice == SyncChoice.Local ? baselineLocal : remote;
        var retained  = preferred ?? baselineLocal ?? remote ?? baseline;
        return preferred ?? (retained is null ? null : retained with { IsDeleted = true });
    }

    private static TValue Pick<TValue>(TValue baseline, TValue local, TValue remote, SyncChoice? choice, ref bool conflict)
    {
        var comparer = EqualityComparer<TValue>.Default;
        if (comparer.Equals(local, remote) || comparer.Equals(remote, baseline)) return local;
        if (comparer.Equals(local, baseline)) return remote;
        conflict = true;
        return choice == SyncChoice.Cloud ? remote : local;
    }

    private static (string Category, string PreviousCategory, bool IsWatched) Watch(SyncItem item)
        => (item.Category, item.PreviousCategory, item.IsWatched);

    private static (string StreamingService, string DeepLinkUri) Streaming(SyncItem item)
        => (item.StreamingService, item.DeepLinkUri);

    private static (int MovieId, string ApiSource, string Type, string Title) IdentityKey(SyncItem item)
        => (item.MovieId, item.ApiSource, item.Type, item.Title);

    private static (int MovieId, string ApiSource, string Type, string Title, string Overview, string PosterUrl,
                    string AvailableStreamingServices, string AggregatedDataJson) Identity(SyncItem item)
    {
        return (item.MovieId, item.ApiSource, item.Type, item.Title, item.Overview, item.PosterUrl,
                item.AvailableStreamingServices, item.AggregatedDataJson);
    }
}
