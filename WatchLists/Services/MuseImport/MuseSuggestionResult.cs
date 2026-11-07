namespace WatchLists.Services.MuseImport;

public sealed record MuseSuggestionResult(MuseRecommendation                   Recommendation
                                        , IReadOnlyList<MuseProviderCandidate> Candidates
                                        , string?                              Error)
{
    public bool NeedsAttention
    {
        get
        {
            return Error != null || new MuseSuggestedMatchSelector().SelectUnambiguous(Recommendation, Candidates) == null;
        }
    }
}
