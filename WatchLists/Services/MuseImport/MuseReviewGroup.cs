namespace WatchLists.Services.MuseImport;

public sealed class MuseReviewGroup
{
    public MuseRecommendation Representative { get; }
    public IReadOnlyList<MuseRecommendation> Members { get; }
    public bool IsGrouped => Members.Count > 1;
    public string GroupingLabel => IsGrouped ? $"{Members.Count} document entries grouped" : string.Empty;

    public MuseReviewGroup(MuseRecommendation representative
                         , IEnumerable<MuseRecommendation> members)
    {
        Representative = representative;
        Members        = Array.AsReadOnly(members.ToArray());
    }
}
