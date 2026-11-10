using System.Text.Json;

namespace WatchLists.Services.MuseImport;

public static class RecommendationMetadata
{
    private static readonly RecommendationField[] SearchFields =
    [
        RecommendationField.Recommender
      , RecommendationField.Context
      , RecommendationField.Rationale
      , RecommendationField.Commentary
      , RecommendationField.Characteristics
      , RecommendationField.Comparisons
      , RecommendationField.BasicDescription
    ];

    public static IReadOnlyList<string> Sentiments { get; } = ["Neutral or unknown", "Positive", "Mixed", "Negative"];

    public static Dictionary<RecommendationField, string> Read(string? json)
    {
        if (json is null) return [];
        var values = JsonSerializer.Deserialize<Dictionary<RecommendationField, string>>(json)
                  ?? throw new InvalidDataException("Recommendation metadata is missing.");
        Validate(values);
        return values;
    }

    public static string? Write(IReadOnlyDictionary<RecommendationField, string> values)
    {
        Validate(values);
        return values.Count == 0 ? null : JsonSerializer.Serialize(new SortedDictionary<RecommendationField, string>(
            values.ToDictionary(field => field.Key, field => field.Value)));
    }

    public static Dictionary<RecommendationField, string> Effective(MovieRecommendation recommendation)
    {
        var values = Read(recommendation.OriginalMetadata);
        if (!values.ContainsKey(RecommendationField.SourceUrl) && !string.IsNullOrWhiteSpace(recommendation.SourceUrl))
            values.Add(RecommendationField.SourceUrl, recommendation.SourceUrl);
        var descriptions = JsonSerializer.Deserialize<string[]>(recommendation.DescriptionsJson) ?? [];
        if (descriptions.Length > 0)
            values.TryAdd(RecommendationField.BasicDescription, string.Join("\n", descriptions));
        foreach (var edit in Read(recommendation.EditedMetadata))
            values[edit.Key] = edit.Value;
        return values;
    }

    public static bool HasSpoilers(MovieRecommendation recommendation)
    {
        // Preserve the source warning even if an edited copy clears it.
        return Read(recommendation.OriginalMetadata).GetValueOrDefault(RecommendationField.ContainsSpoilers) == "true"
            || Read(recommendation.EditedMetadata).GetValueOrDefault(RecommendationField.ContainsSpoilers) == "true";
    }

    public static IEnumerable<string> SearchValues(MovieRecommendation recommendation)
    {
        if (recommendation.IsRemoved == true) return [];
        var values = Effective(recommendation);
        if (HasSpoilers(recommendation))
            return [values.GetValueOrDefault(RecommendationField.Recommender) ?? string.Empty];

        return SearchFields.Select(field => values.GetValueOrDefault(field) ?? string.Empty);
    }

    private static void Validate(IReadOnlyDictionary<RecommendationField, string> values)
    {
        foreach (var (field, value) in values)
        {
            if (!Enum.IsDefined(field) || value is null)
                throw new InvalidDataException("Recommendation metadata contains an invalid field.");
            if (field == RecommendationField.SourceUrl && value.Length > 0)
                _ = RecommendationSourceKey.Create(value);
            if (field == RecommendationField.Sentiment && !Sentiments.Contains(value)
                || field == RecommendationField.ContainsSpoilers && value is not ("true" or "false"))
                throw new InvalidDataException("Recommendation metadata contains an invalid value.");
        }
    }
}
