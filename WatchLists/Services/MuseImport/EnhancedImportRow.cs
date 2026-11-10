using WatchLists.MVVM.Models;

namespace WatchLists.Services.MuseImport;

public sealed record EnhancedImportRow(EnhancedRecommendation Entry, WatchItem? Target, MovieRecommendation? Existing)
{
    public IReadOnlyList<RecommendationField> Conflicts()
    {
        var original = RecommendationMetadata.Read(Existing?.OriginalMetadata);
        var edits    = RecommendationMetadata.Read(Existing?.EditedMetadata);
        return Entry.Metadata.Where(field => original.GetValueOrDefault(field.Key) != field.Value
                                          && (original.ContainsKey(field.Key)
                                           || edits.TryGetValue(field.Key, out var edited) && edited != field.Value))
                             .Select(field => field.Key)
                             .ToArray();
    }

    public bool HasChanges()
    {
        if (Target is null || Existing?.IsRemoved == true) return false;
        var original = RecommendationMetadata.Read(Existing?.OriginalMetadata);
        return Entry.Metadata.Any(field => original.GetValueOrDefault(field.Key) != field.Value);
    }
}
