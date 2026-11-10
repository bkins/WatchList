using System.Text.RegularExpressions;
using WatchLists.MVVM.Models;

namespace WatchLists.Services.MuseImport;

public sealed class EnhancedImportReview(IEnumerable<WatchItem> items, IEnumerable<MovieRecommendation> recommendations)
{
    private readonly Dictionary<Guid, WatchItem> _active = items.Where(item => !item.IsDeleted).ToDictionary(item => item.Id);
    private readonly Dictionary<(Guid, string), MovieRecommendation> _sources =
        recommendations.ToDictionary(row => (row.WatchItemId, row.SourceKey));

    public EnhancedImportRow Preview(EnhancedRecommendation entry, Guid? approvedItem = null)
    {
        var target = approvedItem.HasValue ? _active.GetValueOrDefault(approvedItem.Value) : EstablishedMatch(entry);
        var source = target is null ? null : _sources.GetValueOrDefault((target.Id, entry.SourceKey));
        return new EnhancedImportRow(entry, target, source);
    }

    public IReadOnlyList<WatchItem> Candidates(EnhancedRecommendation entry, string? search)
    {
        var query = string.IsNullOrWhiteSpace(search) ? entry.Title : search.Trim();
        return _active.Values.Where(item => item.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                      .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                      .ThenBy(item => item.Id)
                      .ToArray();
    }

    private WatchItem? EstablishedMatch(EnhancedRecommendation entry)
    {
        if (entry.ReviewNote.Length > 0) return null;
        var candidates = _active.Values.Where(item => item.Title.Trim().Equals(entry.Title.Trim(), StringComparison.OrdinalIgnoreCase)
                                                   && _sources.ContainsKey((item.Id, entry.SourceKey))
                                                   && !HasContradictoryIdentity(entry, item))
                                       .Take(2)
                                       .ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static bool HasContradictoryIdentity(EnhancedRecommendation entry, WatchItem item)
    {
        var type = entry.YearAndType.Split(" — ", 2).Last();
        var movie = type.StartsWith("film", StringComparison.OrdinalIgnoreCase);
        var show = type.StartsWith("TV", StringComparison.OrdinalIgnoreCase);
        if (movie && item.Type.Equals("Show", StringComparison.OrdinalIgnoreCase)
            || show && item.Type.Equals("Movie", StringComparison.OrdinalIgnoreCase))
            return true;

        var year = Regex.Match(entry.YearAndType, @"^\d{4}\b").Value;
        return year.Length > 0 && item.AggregatedData.Any(field =>
            (field.Key is "Year" or "ReleaseDate" or "Release Date" or "First Air Date")
            && Regex.IsMatch(field.Value ?? string.Empty, @"^\d{4}\b")
            && !field.Value!.StartsWith(year, StringComparison.Ordinal));
    }
}
