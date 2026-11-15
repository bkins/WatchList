using WatchLists.MVVM.Models;

namespace WatchLists.Services.MuseImport;

public sealed class MuseUnverifiedImportComparer
{
    public IReadOnlyList<MuseRecommendation> FindUnmatched(IReadOnlyList<MuseRecommendation> recommendations
                                                         , IReadOnlyList<WatchItem> watchItems)
    {
        ArgumentNullException.ThrowIfNull(recommendations);
        ArgumentNullException.ThrowIfNull(watchItems);

        var savedTitles = watchItems.Where(item => !item.IsDeleted)
                                    .Select(item => NormalizeTitle(item.Title))
                                    .Where(title => title.Length > 0)
                                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return recommendations.Where(recommendation => !savedTitles.Contains(NormalizeTitle(recommendation.Title)))
                              .ToList();
    }

    private static string NormalizeTitle(string title)
    {
        return string.Join(' ', title.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
