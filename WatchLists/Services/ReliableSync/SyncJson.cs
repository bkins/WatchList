using System.Text.Json;
using System.Text.Json.Serialization;
using WatchLists.Services.MuseImport;

namespace WatchLists.Services.ReliableSync;

public static class SyncJson
{
    private static readonly JsonSerializerOptions Options = new()
                                                            {
                                                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                                              , RespectRequiredConstructorParameters = true
                                                              , UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
                                                            };

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string text)
    {
        return JsonSerializer.Deserialize<T>(text, Options) ?? throw new InvalidDataException("Sync data is missing.");
    }

    public static bool Equal(SyncSnapshot first, SyncSnapshot second)
    {
        return Write(first.Ordered()) == Write(second.Ordered());
    }

    public static void Validate(SyncSnapshot snapshot)
    {
        if (snapshot.Items is null || snapshot.Links is null)
            throw new InvalidDataException("Sync snapshot is missing its items or recommendations.");

        var itemIds = new HashSet<Guid>();
        foreach (var item in snapshot.Items)
        {
            if (item is null || item.Id == Guid.Empty || !itemIds.Add(item.Id) || item.LastUpdated == default
                || item.Title is null || item.Category is null || item.PreviousCategory is null
                || item.StreamingService is null || item.DeepLinkUri is null || item.ApiSource is null
                || item.Type is null || item.Overview is null || item.PosterUrl is null
                || item.AvailableStreamingServices is null || item.AggregatedDataJson is null)
                throw new InvalidDataException("Sync snapshot contains an invalid or duplicate item.");
        }

        var linkIds  = new HashSet<Guid>();
        var linkKeys = new HashSet<(Guid, string)>();
        foreach (var link in snapshot.Links)
        {
            if (link is null || link.Id == Guid.Empty || !linkIds.Add(link.Id)
                || !itemIds.Contains(link.WatchItemId) || string.IsNullOrWhiteSpace(link.SourceKey)
                || !linkKeys.Add((link.WatchItemId, link.SourceKey))
                || link.Descriptions is not { Length: > 0 } || link.Descriptions.Any(string.IsNullOrWhiteSpace)
                || link.FirstImportedUtc == default || link.LastSeenUtc < link.FirstImportedUtc
                || !ValidSource(link))
                throw new InvalidDataException("Sync snapshot contains an invalid or orphan recommendation.");
        }
    }

    private static bool ValidSource(SyncLink link)
    {
        try
        {
            return RecommendationSourceKey.Create(link.SourceUrl) == link.SourceKey;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
