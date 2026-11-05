using SQLite;
using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseSourceInspectorTests
{
    [Fact]
    public void Find_SharedSource_ShowsEveryAssociatedItemWithoutAssumingIdentity()
    {
        var first = new WatchItem { Title = "Film one" };
        var second = new WatchItem
                     {
                         Title     = "Film two"
                       , IsDeleted = true
                     };
        var row = new MuseRecommendation("Film one", null, "note", "Films", ["https://www.instagram.com/reel/shared/?tracking=1"], 1);
        var saved = new[]
                    {
                        new MovieRecommendation
                        {
                            WatchItemId = first.Id
                          , SourceUrl   = "https://instagram.com/reel/shared"
                          , SourceKey   = RecommendationSourceKey.Create("https://instagram.com/reel/shared")
                        }
                      , new MovieRecommendation
                        {
                            WatchItemId = second.Id
                          , SourceUrl   = "https://instagram.com/reel/shared"
                          , SourceKey   = RecommendationSourceKey.Create("https://instagram.com/reel/shared")
                        }
                    };

        var associations = new MuseSourceInspector().Find(row, saved, [first, second]);

        Assert.Equal(2, associations.Count);
        Assert.Contains(associations, association => association.WatchItemId == second.Id && association.IsDeleted);
    }

    [Fact]
    public void ReadExisting_MissingTable_ReturnsEmptyWithoutCreatingTable()
    {
        SQLitePCL.Batteries_V2.Init();
        using var connection = new SQLiteConnection(":memory:");

        var result = RecommendationStore.ReadExisting(connection);

        Assert.Empty(result);
        Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'MovieRecommendation'"));
    }

    [Fact]
    public void ReadExisting_ClosedConnection_ThrowsInsteadOfReportingEmpty()
    {
        SQLitePCL.Batteries_V2.Init();
        var connection = new SQLiteConnection(":memory:");
        connection.Close();

        Assert.ThrowsAny<Exception>(() => RecommendationStore.ReadExisting(connection));
    }
}
