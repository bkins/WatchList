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
                            , Category  = "Consider Watching"
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
                            , Category  = "Consider Watching"
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
                         , Category  = "Consider Watching"
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
                         , Category  = "Consider Watching"
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
    [Fact]
    public void Apply_LargeReviewedBatchAndRepeat_PreservesExistingDataAndAssociations()
    {
        SQLitePCL.Batteries_V2.Init();
        var folder = Path.Combine(Path.GetTempPath(), "WatchListImportTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var connection = new SQLiteConnection(Path.Combine(folder, "watchlist.db"));
            connection.CreateTable<WatchItem>();
            var existing = new WatchItem
                           {
                               Title            = "Existing synthetic show"
                             , MovieId          = 1000
                             , ApiSource        = "TVMaze"
                             , Category         = "Finished Watching"
                             , IsWatched        = true
                             , IsLiked          = true
                             , StreamingService = "Example service"
                           };
            connection.Insert(existing);
            var rows = Enumerable.Range(0, 201)
                                 .Select(index => new MuseRecommendation($"Synthetic title {index}"
                                                                        , null
                                                                        , $"Synthetic description {index}"
                                                                        , "Recommendations"
                                                                        , [index < 3 ? "https://example.com/shared" : $"https://example.com/{index}"]
                                                                        , index + 1))
                                 .ToList();
            var review = new MuseImportReview(rows);
            review.ApproveExisting(0, existing);
            for (var index = 1; index < rows.Count; index++)
            {
                review.ApproveNew(index, new WatchItem
                                         {
                                             Title     = rows[index].Title
                                           , MovieId   = index + 1000
                                           , ApiSource = "TMDB"
                                           , Category  = "Consider Watching"
                                         });
            }
            var firstBatch = review.CreateBatch();
            var coordinator = new MuseImportCoordinator(connection, () => false);

            var first = coordinator.Apply(firstBatch, folder, DateTime.UtcNow);
            var savedItems = connection.Table<WatchItem>().ToList();
            var secondReview = new MuseImportReview(rows);
            secondReview.ApproveExisting(0, existing);
            for (var index = 1; index < rows.Count; index++)
            {
                secondReview.ApproveExisting(index, savedItems.Single(item => item.Title == rows[index].Title));
            }
            var second = coordinator.Apply(secondReview.CreateBatch(), folder, DateTime.UtcNow);

            Assert.Equal(200, first.NewItems);
            Assert.Equal(201, first.NewRecommendations);
            Assert.Equal(0, second.NewItems);
            Assert.Equal(0, second.NewRecommendations);
            Assert.Equal(201, connection.Table<WatchItem>().Count());
            Assert.Equal(201, new RecommendationStore(connection).GetAll().Count);
            var preserved = connection.Find<WatchItem>(existing.Id);
            Assert.True(preserved.IsWatched);
            Assert.True(preserved.IsLiked);
            Assert.Equal("Finished Watching", preserved.Category);
            Assert.Equal("Example service", preserved.StreamingService);
            using var backup = new SQLiteConnection(first.BackupPath);
            Assert.Single(backup.Table<WatchItem>().ToList());
            Assert.True(backup.Find<WatchItem>(existing.Id).IsWatched);
            using var restored = new SQLiteConnection(second.BackupPath);
            var restoredSources = new RecommendationStore(restored).GetAll();
            Assert.Equal(201, restoredSources.Count);
            Assert.Equal(3, restoredSources.Count(source => source.SourceUrl == "https://example.com/shared"));
            Assert.Contains(restoredSources, source => source.WatchItemId == existing.Id);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
