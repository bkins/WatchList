using System.Text.Json;
using SQLite;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class RecommendationStoreTests
{
    [Fact]
    public void AddOrUpdate_RepeatedSourceForSameMovie_PreservesDistinctDescriptions()
    {
        SQLitePCL.Batteries_V2.Init();
        using var connection = new SQLiteConnection(":memory:");
        var store = new RecommendationStore(connection);
        var movieId = Guid.NewGuid();
        connection.CreateTable<TestWatchItem>();
        connection.Insert(new TestWatchItem { Id = movieId });
        var firstSeen = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(1, store.Apply([new(movieId, "https://www.instagram.com/reel/Example/?tracking=one", "First")], firstSeen));
        var insertedAgain = store.Apply([new(movieId, "https://instagram.com/reel/Example?tracking=two", "Second")], firstSeen.AddDays(1));
        store.Apply([new(movieId, "https://instagram.com/reel/Example", "Second")], firstSeen.AddDays(2));

        var recommendation = Assert.Single(store.GetAll());
        Assert.Equal(0, insertedAgain);
        Assert.Equal(new[] { "First", "Second" }, JsonSerializer.Deserialize<string[]>(recommendation.DescriptionsJson));
        Assert.Equal(firstSeen, recommendation.FirstImportedUtc);
        Assert.Equal(firstSeen.AddDays(2), recommendation.LastSeenUtc);
    }

    [Fact]
    public void AddOrUpdate_SameSourceForDifferentMovies_KeepsBothAssociations()
    {
        SQLitePCL.Batteries_V2.Init();
        using var connection = new SQLiteConnection(":memory:");
        var store = new RecommendationStore(connection);
        connection.CreateTable<TestWatchItem>();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        connection.Insert(new TestWatchItem { Id = firstId });
        connection.Insert(new TestWatchItem { Id = secondId });
        var now = DateTime.UtcNow;

        store.Apply([new(firstId, "https://instagram.com/reel/shared", "First movie")
                   , new(secondId, "https://instagram.com/reel/shared", "Second movie")], now);

        Assert.Equal(2, store.GetAll().Count);
    }

    [Theory]
    [InlineData("file:///tmp/movie")]
    [InlineData("https://user:password@example.com/item")]
    [InlineData("not a url")]
    public void AddOrUpdate_InvalidSource_RejectsWithoutWriting(string sourceUrl)
    {
        SQLitePCL.Batteries_V2.Init();
        using var connection = new SQLiteConnection(":memory:");
        var store = new RecommendationStore(connection);
        connection.CreateTable<TestWatchItem>();
        var movieId = Guid.NewGuid();
        connection.Insert(new TestWatchItem { Id = movieId });

        Assert.Throws<ArgumentException>(() => store.Apply([new(movieId, sourceUrl, "description")], DateTime.UtcNow));

        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Apply_BatchContainsDeletedMovie_RollsBackEarlierRecommendations()
    {
        SQLitePCL.Batteries_V2.Init();
        using var connection = new SQLiteConnection(":memory:");
        var store = new RecommendationStore(connection);
        connection.CreateTable<TestWatchItem>();
        var activeId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();
        connection.Insert(new TestWatchItem { Id = activeId });
        connection.Insert(new TestWatchItem
                          {
                              Id        = deletedId
                            , IsDeleted = true
                          });
        var entries = new[]
                      {
                          new RecommendationImportEntry(activeId, "https://example.com/one", "First")
                        , new RecommendationImportEntry(deletedId, "https://example.com/two", "Second")
                      };

        Assert.Throws<InvalidOperationException>(() => store.Apply(entries, DateTime.UtcNow));

        Assert.Empty(store.GetAll());
    }
}
