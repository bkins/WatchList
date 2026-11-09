using WatchLists.MVVM.Models;

namespace WatchLists.Services.ReliableSync;

public sealed record SyncItem(Guid     Id
                            , string   Title
                            , string   Category
                            , string   PreviousCategory
                            , bool     IsWatched
                            , bool     IsLiked
                            , bool     IsDeleted
                            , string   StreamingService
                            , string   DeepLinkUri
                            , int      MovieId
                            , string   ApiSource
                            , string   Type
                            , string   Overview
                            , string   PosterUrl
                            , string   AvailableStreamingServices
                            , string   AggregatedDataJson
                            , DateTime LastUpdated)
{
    public static SyncItem From(WatchItem item)
    {
        return new SyncItem(item.Id
                          , item.Title ?? string.Empty
                          , item.Category ?? string.Empty
                          , item.PreviousCategory ?? string.Empty
                          , item.IsWatched
                          , item.IsLiked
                          , item.IsDeleted
                          , item.StreamingService ?? string.Empty
                          , item.DeepLinkUri ?? string.Empty
                          , item.MovieId
                          , item.ApiSource ?? string.Empty
                          , item.Type ?? string.Empty
                          , item.Overview ?? string.Empty
                          , item.PosterUrl ?? string.Empty
                          , item.AvailableStreamingServices ?? string.Empty
                          , item.AggregatedDataJson ?? string.Empty
                          , new DateTime(item.LastUpdated.Ticks, DateTimeKind.Utc));
    }

    public WatchItem ToWatchItem()
    {
        return new WatchItem
               {
                   Id                         = Id
                 , Title                      = Title
                 , Category                   = Category
                 , PreviousCategory           = PreviousCategory
                 , IsWatched                  = IsWatched
                 , IsLiked                    = IsLiked
                 , IsDeleted                  = IsDeleted
                 , StreamingService           = StreamingService
                 , DeepLinkUri                = DeepLinkUri
                 , MovieId                    = MovieId
                 , ApiSource                  = ApiSource
                 , Type                       = Type
                 , Overview                   = Overview
                 , PosterUrl                  = PosterUrl
                 , AvailableStreamingServices = AvailableStreamingServices
                 , AggregatedDataJson         = AggregatedDataJson
                 , LastUpdated                = LastUpdated
               };
    }
}
