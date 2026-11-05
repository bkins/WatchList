using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseImportReviewTests
{
    [Fact]
    public void CreateBatch_OnlyApprovedRows_AreIncludedWithEverySource()
    {
        var first = new MuseRecommendation("First", 2020, "note", "Films", ["https://example.com/one", "https://example.com/two"], 1);
        var second = new MuseRecommendation("Second", null, "other", "Films", ["https://example.com/three"], 3);
        var review = new MuseImportReview([first, second]);
        var existing = new WatchItem
                       {
                           Title     = "First"
                         , IsWatched = true
                       };
        review.ApproveExisting(0, existing);

        var batch = review.CreateBatch();

        Assert.Empty(batch.NewItems);
        Assert.Equal(2, batch.Recommendations.Count);
        Assert.All(batch.Recommendations, entry => Assert.Equal(existing.Id, entry.WatchItemId));
        Assert.True(existing.IsWatched);
    }

    [Fact]
    public void ApproveNew_SameProviderIdentityForTwoRows_CreatesOneNewWatchItem()
    {
        var first = new MuseRecommendation("Film", null, "first", "Films", ["https://example.com/one"], 1);
        var second = new MuseRecommendation("Film", null, "second", "Films", ["https://example.com/two"], 2);
        var review = new MuseImportReview([first, second]);
        var firstItem = new WatchItem
                        {
                            Title     = "Film"
                          , MovieId   = 123
                          , ApiSource = "TMDB"
                        };
        var secondItem = new WatchItem
                         {
                             Title     = "Film"
                           , MovieId   = 123
                           , ApiSource = "tmdb"
                         };

        review.ApproveNew(0, firstItem);
        review.ApproveNew(1, secondItem);
        var batch = review.CreateBatch();

        Assert.Single(batch.NewItems);
        Assert.Equal(2, batch.Recommendations.Count);
        Assert.All(batch.Recommendations, entry => Assert.Equal(firstItem.Id, entry.WatchItemId));
    }

    [Fact]
    public void ApproveExisting_DeletedItem_RejectsWithoutApproval()
    {
        var row = new MuseRecommendation("Film", null, "note", "Films", ["https://example.com/one"], 1);
        var review = new MuseImportReview([row]);
        var deleted = new WatchItem { IsDeleted = true };

        Assert.Throws<InvalidOperationException>(() => review.ApproveExisting(0, deleted));

        Assert.Equal(0, review.ApprovedCount);
    }

    [Fact]
    public void ClearApproval_LastReferenceToNewItem_RemovesItFromBatch()
    {
        var first = new MuseRecommendation("First", null, "first", "Films", ["https://example.com/one"], 1);
        var second = new MuseRecommendation("Second", null, "second", "Films", ["https://example.com/two"], 2);
        var review = new MuseImportReview([first, second]);
        var proposed = new WatchItem
                       {
                           Title     = "First"
                         , MovieId   = 123
                         , ApiSource = "TMDB"
                       };
        review.ApproveNew(0, proposed);
        review.ApproveExisting(1, new WatchItem { Title = "Second" });

        review.ClearApproval(0);
        var batch = review.CreateBatch();

        Assert.Empty(batch.NewItems);
        Assert.Single(batch.Recommendations);
    }
}
