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

    [Fact]
    public void Apply_RepeatReviewedImport_DoesNotDuplicateItemOrSource()
    {
        SQLitePCL.Batteries_V2.Init();
        var folder = Path.Combine(Path.GetTempPath(), "WatchListImportTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var connection = new SQLiteConnection(Path.Combine(folder, "watchlist.db"));
            connection.CreateTable<WatchItem>();
            var parser = new MuseMarkdownParser();
            var rows = parser.Parse("## Films\n- **Film** — A note\n  Source: [video](https://instagram.com/reel/one)").Recommendations;
            var item = new WatchItem
                       {
                           Title     = "Film"
                         , MovieId   = 456
                         , ApiSource = "TMDB"
                       };
            var firstReview = new MuseImportReview(rows);
            firstReview.ApproveNew(0, item);
            var coordinator = new MuseImportCoordinator(connection, () => false);
            var first = coordinator.Apply(firstReview.CreateBatch(), folder, DateTime.UtcNow);
            var saved = connection.Find<WatchItem>(item.Id);
            saved.IsWatched = true;
            connection.Update(saved);
            var nextReview = new MuseImportReview(rows);
            nextReview.ApproveExisting(0, saved);

            var second = coordinator.Apply(nextReview.CreateBatch(), folder, DateTime.UtcNow);

            Assert.Equal(1, first.NewItems);
            Assert.Equal(0, second.NewItems);
            Assert.Equal(0, second.NewRecommendations);
            Assert.Single(connection.Table<WatchItem>().ToList());
            Assert.True(connection.Find<WatchItem>(item.Id).IsWatched);
            Assert.Single(new RecommendationStore(connection).GetAll());
            Assert.NotEqual(first.BackupPath, second.BackupPath);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
