using System.Text.Json;
using WatchLists.Services.MuseImport;

namespace WatchLists.Services.ReliableSync;

public sealed record SyncLink(Guid     Id
                            , Guid     WatchItemId
                            , string   SourceKey
                            , string   SourceUrl
                            , string[] Descriptions
                            , DateTime FirstImportedUtc
                            , DateTime LastSeenUtc)
{
    public static SyncLink From(MovieRecommendation link)
    {
        return new SyncLink(link.Id
                          , link.WatchItemId
                          , link.SourceKey
                          , link.SourceUrl
                          , JsonSerializer.Deserialize<string[]>(link.DescriptionsJson) ?? []
                          , new DateTime(link.FirstImportedUtc.Ticks, DateTimeKind.Utc)
                          , new DateTime(link.LastSeenUtc.Ticks, DateTimeKind.Utc));
    }

    public MovieRecommendation ToRecommendation()
    {
        return new MovieRecommendation
               {
                   Id               = Id
                 , WatchItemId      = WatchItemId
                 , SourceKey        = SourceKey
                 , SourceUrl        = SourceUrl
                 , DescriptionsJson = JsonSerializer.Serialize(Descriptions)
                 , FirstImportedUtc = FirstImportedUtc
                 , LastSeenUtc      = LastSeenUtc
               };
    }
}
