using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class UnverifiedMuseRecommendationStoreTests
{
    [Fact]
    public void AddOrUpdate_PersistsSkippedEntriesAndDeduplicatesBySource()
    {
        using var database = new DisposableImportDatabase();
        var store = new UnverifiedMuseRecommendationStore(database.Connection);
        var recommendation = new MuseRecommendation("Unmatched film", 1984, "A note", "Films",
            ["https://example.com/source"], 12);

        var firstCount = store.AddOrUpdate([recommendation], DateTime.UtcNow);
        var repeatCount = store.AddOrUpdate([recommendation], DateTime.UtcNow.AddMinutes(1));

        Assert.Equal(1, firstCount);
        Assert.Equal(0, repeatCount);
        var saved = Assert.Single(store.GetAll());
        Assert.Equal("Unmatched film", saved.Title);
        Assert.Equal(1984, saved.ReleaseYear);
        Assert.Equal("https://example.com/source", saved.SourceUrl);
        Assert.Equal(12, saved.SourceLineNumber);
    }

    [Fact]
    public void AddOrUpdate_PreservesEntriesWithoutSourceLinks()
    {
        using var database = new DisposableImportDatabase();
        var store = new UnverifiedMuseRecommendationStore(database.Connection);
        var recommendation = new MuseRecommendation("Unmatched film", null, "A note", "Films", [], 12);

        store.AddOrUpdate([recommendation], DateTime.UtcNow);

        var saved = Assert.Single(store.GetAll());
        Assert.Equal("Unmatched film", saved.Title);
        Assert.Empty(saved.SourceUrl);
    }

    [Fact]
    public void AddOrUpdate_SharedSourceForDifferentTitles_PreservesBothEntries()
    {
        using var database = new DisposableImportDatabase();
        var store = new UnverifiedMuseRecommendationStore(database.Connection);
        var source = "https://example.com/shared";
        var recommendations = new[]
                              {
                                  new MuseRecommendation("Film One", 2000, "note one", "Films", [source], 1)
                                , new MuseRecommendation("Film Two", 2001, "note two", "Films", [source], 2)
                              };

        store.AddOrUpdate(recommendations, DateTime.UtcNow);

        Assert.Equal(new[] { "Film One", "Film Two" }, store.GetAll().Select(item => item.Title).OrderBy(title => title));
    }
}
