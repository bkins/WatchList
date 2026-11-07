namespace WatchLists.Services.MuseImport;

public sealed class MuseSuggestedMatchSelector
{
    public MuseExistingItem? FindExistingIdentity(MuseProviderCandidate candidate, IReadOnlyList<MuseExistingItem> existingItems)
    {
        var matches = existingItems.Where(item => item.MovieId == candidate.Id &&
                                                 string.Equals(item.ApiSource, candidate.Provider, StringComparison.OrdinalIgnoreCase))
                                   .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    public MuseProviderCandidate? SelectUnambiguous(MuseRecommendation row, IReadOnlyList<MuseProviderCandidate> candidates)
    {
        var exactMatches = candidates.Where(candidate => string.Equals(row.Title.Trim(), candidate.Title.Trim(), StringComparison.OrdinalIgnoreCase))
                                     .Where(candidate => !row.ReleaseYear.HasValue || MatchesYear(candidate.Date, row.ReleaseYear.Value))
                                     .ToList();
        return exactMatches.Count == 1 ? exactMatches[0] : null;
    }

    private static bool MatchesYear(string date, int expectedYear)
    {
        return date.Length >= 4 && int.TryParse(date.AsSpan(0, 4), out var year) && year == expectedYear;
    }
}
