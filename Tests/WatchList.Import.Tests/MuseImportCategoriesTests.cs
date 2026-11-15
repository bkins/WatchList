using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseImportCategoriesTests
{
    [Fact]
    public void GetAvailable_ContainsConfiguredWatchedCategory()
    {
        var categories = new[]
                         {
                             "Currently Watching"
                           , "Finished Watching"
                           , "Consider Watching"
                         };

        var result = MuseImportCategories.GetAvailable(categories);

        Assert.Contains("Consider Watching", result);
        Assert.Equal(categories, result);
    }

    [Fact]
    public void GetDefault_WithoutPreviousSelection_UsesConsiderWatching()
    {
        var categories = new[]
                         {
                             "Currently Watching"
                           , "Finished Watching"
                           , "Consider Watching"
                         };

        var result = MuseImportCategories.GetDefault(categories, null);

        Assert.Equal("Consider Watching", result);
    }
}
