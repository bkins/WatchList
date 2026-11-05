using SQLite;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseImportCoordinatorTests
{
    [Fact]
    public void Apply_AutoSyncEnabled_StopsBeforeCreatingBackupOrDatabaseChanges()
    {
        SQLitePCL.Batteries_V2.Init();
        using var connection = new SQLiteConnection(":memory:");
        var directory = Path.Combine(Path.GetTempPath(), "WatchListImportTests", Guid.NewGuid().ToString("N"));
        var coordinator = new MuseImportCoordinator(connection, () => true);
        var batch = new MuseImportBatch([], []);

        Assert.Throws<InvalidOperationException>(() => coordinator.Apply(batch, directory, DateTime.UtcNow));

        Assert.False(Directory.Exists(directory));
        Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'MovieRecommendation'"));
    }
}
