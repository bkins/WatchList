namespace WatchLists.Services.MuseImport;

public sealed record MuseSuggestionResult(MuseRecommendation Recommendation
                                        , IReadOnlyList<MuseProviderCandidate> Candidates
                                        , string? Error);
