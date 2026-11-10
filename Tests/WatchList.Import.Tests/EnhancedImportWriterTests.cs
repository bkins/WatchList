using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;
using WatchLists.Services.ReliableSync;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class EnhancedImportWriterTests
{
    [Fact]
    public void Apply_ConflictingSourcePreservesEditAndEnrichesIndependentFields()
    {
        using var database = new DisposableImportDatabase();
        var item = new WatchItem { Title = "Coherence", Category = "My categories", IsWatched = true, IsLiked = true };
        database.Connection.Insert(item);
        var url = "https://www.instagram.com/reel/First/";
        new RecommendationStore(database.Connection).Apply([new(item.Id, url, "Basic source")], DateTime.UtcNow);
        var set = new RecommendationSet(database.Connection);
        var initial = Assert.Single(set.Read(item.Id));
        set.Save(item.Id, initial.Id, new Dictionary<RecommendationField, string> { [RecommendationField.Rationale] = "My correction" }, null, null);
        var incoming = new EnhancedRecommendation("Coherence", "2013 — film", url, RecommendationSourceKey.Create(url)
                                                , new Dictionary<RecommendationField, string>
                                                  {
                                                      [RecommendationField.Rationale] = "Source reason"
                                                    , [RecommendationField.Context]   = "Source premise"
                                                  }
                                                , string.Empty);
        var review = new EnhancedImportReview([item], set.Read(item.Id)).Preview(incoming);
        var writer = new EnhancedImportWriter(database.Connection);

        var receipt = writer.Apply([new(review, new HashSet<RecommendationField> { RecommendationField.Rationale })], database.DirectoryPath);
        var saved = Assert.Single(set.Read(item.Id));
        var repeated = new EnhancedImportReview([item], [saved]).Preview(incoming);

        Assert.Equal([RecommendationField.Rationale], review.Conflicts());
        Assert.Equal(1, receipt.Enriched);
        Assert.True(File.Exists(receipt.BackupPath));
        Assert.Equal(initial.Id, saved.Id);
        Assert.Equal("Source reason", RecommendationMetadata.Read(saved.OriginalMetadata)[RecommendationField.Rationale]);
        Assert.Equal("My correction", RecommendationMetadata.Effective(saved)[RecommendationField.Rationale]);
        Assert.Equal("Source premise", RecommendationMetadata.Effective(saved)[RecommendationField.Context]);
        Assert.False(repeated.HasChanges());
        Assert.Empty(repeated.Conflicts());
        Assert.Equal("My categories", database.Connection.Find<WatchItem>(item.Id).Category);
        Assert.True(database.Connection.Find<WatchItem>(item.Id).IsWatched);
        Assert.True(database.Connection.Find<WatchItem>(item.Id).IsLiked);
    }

    [Fact]
    public void Apply_StaleReview_RollsBackAllRows()
    {
        using var database = new DisposableImportDatabase();
        var item = new WatchItem { Title = "Coherence" };
        database.Connection.Insert(item);
        var first = new EnhancedRecommendation("Coherence", "2013 — film", "https://example.test/one", "https://example.test/one"
                                             , new Dictionary<RecommendationField, string> { [RecommendationField.Context] = "First" }, string.Empty);
        var second = first with { SourceUrl = "https://example.test/two", SourceKey = "https://example.test/two" };
        var review = new EnhancedImportReview([item], []);
        var approvals = new[]
        {
            new EnhancedImportApproval(review.Preview(first, item.Id), new HashSet<RecommendationField>())
          , new EnhancedImportApproval(review.Preview(second, item.Id), new HashSet<RecommendationField>())
        };
        new RecommendationStore(database.Connection).Apply([new(item.Id, second.SourceUrl, "Other import")], DateTime.UtcNow);
        var before = ReliableSyncStore.ReadLocal(database.Connection);

        Assert.Throws<InvalidOperationException>(() => new EnhancedImportWriter(database.Connection).Apply(approvals, database.DirectoryPath));

        Assert.Null(review.Preview(first).Target);
        Assert.True(SyncJson.Equal(before, ReliableSyncStore.ReadLocal(database.Connection)));
    }
}
