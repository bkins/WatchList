namespace WatchLists.Services.MuseImport;

public sealed class MuseReviewGrouper
{
    private const string SummarySection = "Recommended more than once";

    public IReadOnlyList<MuseReviewGroup> Group(IReadOnlyList<MuseRecommendation> recommendations)
    {
        ArgumentNullException.ThrowIfNull(recommendations);

        var primaryIndexes = Enumerable.Range(0, recommendations.Count)
                                       .Where(index => !IsSummary(recommendations[index]))
                                       .ToList();
        var groupedMembers = new Dictionary<int, List<int>>();
        var groupedIndexes = new HashSet<int>();

        for (var index = 0; index < recommendations.Count; index++)
        {
            var summary = recommendations[index];
            if (!IsSummary(summary) || summary.SourceUrls.Count == 0) continue;

            var candidates = primaryIndexes.Where(primaryIndex => HasSameContent(summary, recommendations[primaryIndex]))
                                           .ToList();
            if (candidates.Count != 1) continue;

            var primaryIndex = candidates[0];
            var primary      = recommendations[primaryIndex];
            if (!summary.SourceUrls.All(source => primary.SourceUrls.Contains(source, StringComparer.Ordinal))) continue;

            if (!groupedMembers.TryGetValue(primaryIndex, out var members))
            {
                members = new List<int> { primaryIndex };
                groupedMembers.Add(primaryIndex, members);
            }
            members.Add(index);
            groupedIndexes.Add(index);
        }

        var groups = new List<MuseReviewGroup>();
        for (var index = 0; index < recommendations.Count; index++)
        {
            if (groupedIndexes.Contains(index)) continue;

            var members = groupedMembers.TryGetValue(index, out var memberIndexes)
                ? memberIndexes.OrderBy(memberIndex => memberIndex).Select(memberIndex => recommendations[memberIndex])
                : new[] { recommendations[index] };
            groups.Add(new MuseReviewGroup(recommendations[index], members));
        }
        return groups.AsReadOnly();
    }

    private static bool IsSummary(MuseRecommendation recommendation) => recommendation.Section == SummarySection;

    private static bool HasSameContent(MuseRecommendation summary, MuseRecommendation primary)
    {
        return summary.Title == primary.Title && summary.ReleaseYear == primary.ReleaseYear &&
               summary.Description == primary.Description;
    }
}
