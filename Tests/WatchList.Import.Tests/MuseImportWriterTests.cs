using SQLite;
using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseImportWriterTests
{
    [Fact]
    public void Apply_ConfirmedItemAndRecommendation_CreatesBackupAndWritesAtomically()
    {
        SQLitePCL.Batteries_V2.Init();
        var folder = Path.Combine(Path.GetTempPath(), "WatchListImportTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var databasePath = Path.Combine(folder, "watchlist.db");
            var backupPath   = Path.Combine(folder, "before.db");
            using var connection = new SQLiteConnection(databasePath);
            connection.CreateTable<WatchItem>();
            var existing = new WatchItem
                           {
                               Title     = "Already watched"
                             , IsWatched = true
                           };
            connection.Insert(existing);
            var newItem = new WatchItem
                          {
                              Title     = "New film"
                            , MovieId   = 123
                            , ApiSource = "TMDB"
                          };
            var batch = new MuseImportBatch([newItem]
                                          , [new(newItem.Id, "https://instagram.com/reel/one", "Recommendation")]);

            var receipt = new MuseImportWriter(connection).Apply(batch, backupPath, DateTime.UtcNow);

            Assert.Equal(1, receipt.NewItems);
            Assert.Equal(1, receipt.NewRecommendations);
            Assert.True(connection.Find<WatchItem>(existing.Id).IsWatched);
            Assert.Single(new RecommendationStore(connection).GetAll());
            using var backup = new SQLiteConnection(backupPath);
            Assert.Single(backup.Table<WatchItem>().ToList());
            Assert.True(backup.Find<WatchItem>(existing.Id).IsWatched);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Apply_DuplicateProviderIdentity_RollsBackBatchAndPreservesBackup()
    {
        SQLitePCL.Batteries_V2.Init();
        var folder = Path.Combine(Path.GetTempPath(), "WatchListImportTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var backupPath = Path.Combine(folder, "before.db");
            using var connection = new SQLiteConnection(Path.Combine(folder, "watchlist.db"));
            connection.CreateTable<WatchItem>();
            connection.Insert(new WatchItem
                              {
                                  Title     = "Deleted film"
                                , MovieId   = 123
                                , ApiSource = "TMDB"
                                , IsDeleted = true
                              });
            var newItem = new WatchItem
                          {
                              Title     = "Same film"
                            , MovieId   = 123
                            , ApiSource = "tmdb"
                          };
            var batch = new MuseImportBatch([newItem], [new(newItem.Id, "https://example.com/source", "Recommendation")]);

            Assert.Throws<InvalidOperationException>(() => new MuseImportWriter(connection).Apply(batch, backupPath, DateTime.UtcNow));

            Assert.Single(connection.Table<WatchItem>().ToList());
            Assert.True(File.Exists(backupPath));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Apply_InvalidRecommendation_RollsBackNewItem()
    {
        SQLitePCL.Batteries_V2.Init();
        var folder = Path.Combine(Path.GetTempPath(), "WatchListImportTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var connection = new SQLiteConnection(Path.Combine(folder, "watchlist.db"));
            connection.CreateTable<WatchItem>();
            var item = new WatchItem
                       {
                           Title     = "New film"
                         , MovieId   = 456
                         , ApiSource = "TMDB"
                       };
            var batch = new MuseImportBatch([item], [new(item.Id, "not a URL", "Recommendation")]);

            Assert.Throws<ArgumentException>(() => new MuseImportWriter(connection).Apply(batch
                                                                                       , Path.Combine(folder, "before.db")
                                                                                       , DateTime.UtcNow));

            Assert.Empty(connection.Table<WatchItem>().ToList());
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
