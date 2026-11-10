using WatchLists.Services.MuseImport;

namespace WatchLists.MVVM.Views;

internal static class RecommendationFields
{
    public static string Label(RecommendationField field)
    {
        return field switch
        {
            RecommendationField.SourceType        => "Source"
          , RecommendationField.SourceUrl         => "Source URL"
          , RecommendationField.ViewingInstructions => "Viewing advice"
          , RecommendationField.Rank              => "Rank, score or verdict"
          , RecommendationField.HistoricalStreaming => "Streaming mentioned by the source (historical)"
          , RecommendationField.Confidence        => "Extraction confidence"
          , RecommendationField.ContainsSpoilers  => "Contains spoilers"
          , RecommendationField.BasicDescription  => "Source description"
          , _                                      => field.ToString()
        };
    }

    public static string Name(MovieRecommendation recommendation)
    {
        var values = RecommendationMetadata.Effective(recommendation);
        foreach (var field in new[] { RecommendationField.Recommender, RecommendationField.SourceType })
        {
            if (values.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }
        return recommendation.IsManual ? "Personal recommendation" : "Imported recommendation";
    }
}
