using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseUnverifiedImportComparerTests
{
    [Fact]
    public void FindUnmatched_UsesExactActiveTitlesAndIgnoresDeletedItems()
    {
        var recommendations = new[]
                             {
                                 new MuseRecommendation("Dune", 2021, "note", "Films", ["https://example.com/1"], 1)
                               , new MuseRecommendation("Arrival", 2016, "note", "Films", ["https://example.com/2"], 2)
                               , new MuseRecommendation("Deleted title", 1999, "note", "Films", ["https://example.com/3"], 3)
                             };
        var watchItems = new[]
                         {
                             new WatchItem { Title = "  Dune " }
                           , new WatchItem { Title = "Deleted title", IsDeleted = true }
                         };

        var unmatched = new MuseUnverifiedImportComparer().FindUnmatched(recommendations, watchItems);

        Assert.Equal(new[] { "Arrival", "Deleted title" }, unmatched.Select(item => item.Title));
    }
}
