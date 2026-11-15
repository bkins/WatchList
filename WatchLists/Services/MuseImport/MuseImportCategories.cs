namespace WatchLists.Services.MuseImport;

public static class MuseImportCategories
{
    public static IReadOnlyList<string> GetAvailable(IReadOnlyList<string> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        return categories.ToList();
    }

    public static string? GetDefault(IReadOnlyList<string> categories, string? selectedCategory)
    {
        ArgumentNullException.ThrowIfNull(categories);
        if (categories.Contains(selectedCategory ?? string.Empty)) return selectedCategory;
        return categories.FirstOrDefault(category => string.Equals(category, "Consider Watching", StringComparison.OrdinalIgnoreCase));
    }
}
