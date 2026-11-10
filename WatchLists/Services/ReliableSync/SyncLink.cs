using System.Text.Json;
using System.Text.Json.Serialization;
using WatchLists.Services.MuseImport;

namespace WatchLists.Services.ReliableSync;

public sealed record SyncLink(Guid     Id
                            , Guid     WatchItemId
                            , string   SourceKey
                            , string   SourceUrl
                            , string[] Descriptions
                            , DateTime FirstImportedUtc
                            , DateTime LastSeenUtc
                            , [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OriginalMetadata = null
                            , [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EditedMetadata = null
                            , [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool?   IsPrimary = null
                            , [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool?   IsRemoved = null)
{
    [JsonIgnore]
    public bool IsEnhanced => OriginalMetadata is not null || EditedMetadata is not null
                           || IsPrimary.HasValue || IsRemoved.HasValue
                           || SourceKey.StartsWith("manual:", StringComparison.Ordinal);

    public static SyncLink From(MovieRecommendation link)
    {
        return new SyncLink(link.Id
                          , link.WatchItemId
                          , link.SourceKey
                          , link.SourceUrl
                          , JsonSerializer.Deserialize<string[]>(link.DescriptionsJson) ?? []
                          , new DateTime(link.FirstImportedUtc.Ticks, DateTimeKind.Utc)
                          , new DateTime(link.LastSeenUtc.Ticks, DateTimeKind.Utc)
                          , link.OriginalMetadata
                          , link.EditedMetadata
                          , link.IsPrimary
                          , link.IsRemoved);
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
                 , OriginalMetadata = OriginalMetadata
                 , EditedMetadata   = EditedMetadata
                 , IsPrimary         = IsPrimary
                 , IsRemoved         = IsRemoved
               };
    }
}
