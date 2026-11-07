using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseImportReviewTests
{
    [Fact]
    public void ApproveNew_ReapprovedCategory_KeepsSharedItemAndDecisionsConsistent()
    {
        var review = CreateReview(2);
        review.ApproveNew(0, CreateNewItem());
        review.ApproveNew(1, CreateNewItem());
        var replacement = CreateNewItem();
        replacement.Category = "Currently Watching";

        review.ApproveNew(0, replacement);
        var batch = review.CreateFinalBatch();

        Assert.Equal("Currently Watching", Assert.Single(batch.NewItems).Category);
        Assert.Equal("Currently Watching", review.GetDecision(0).Category);
        Assert.Equal("Currently Watching", review.GetDecision(1).Category);
        Assert.Equal(2, batch.Recommendations.Count);
    }

    [Fact]
    public void CreateFinalBatch_PendingRow_RequiresExplicitDecision()
    {
        var review = CreateReview(2);
        review.ApproveExisting(0, new WatchItem { Title = "Saved film" });

        Assert.Throws<InvalidOperationException>(() => review.CreateFinalBatch());

        Assert.Equal(1, review.GetSummary().PendingRows);
        Assert.Equal(1, review.ApprovedCount);
    }

    [Fact]
    public void SkipPending_MixedDecisions_PreservesApprovalsAndCompletesReview()
    {
        var review = CreateReview(4);
        var existing = new WatchItem { Title = "Saved film" };
        review.ApproveExisting(0, existing);
        review.Skip(1);

        review.SkipPending();
        var batch = review.CreateFinalBatch();

        Assert.Equal(3, review.GetSummary().SkippedRows);
        Assert.Equal(0, review.GetSummary().PendingRows);
        Assert.Equal(existing.Id, review.GetDecision(0).ItemId);
        Assert.Single(batch.Recommendations);
    }

    [Fact]
    public void GetDecision_ApprovedNewRow_RetainsIdentityAndCategoryForRevisit()
    {
        var review = CreateReview(1);
        var item = CreateNewItem();

        review.ApproveNew(0, item);
        var decision = review.GetDecision(0);

        Assert.Equal(MuseReviewDecisionStatus.ApprovedNew, decision.Status);
        Assert.Equal(item.Id, decision.ItemId);
        Assert.Equal(item.MovieId, decision.ProviderId);
        Assert.Equal(item.ApiSource, decision.Provider);
        Assert.Equal(item.Title, decision.ItemTitle);
        Assert.Equal(item.Type, decision.MediaType);
        Assert.Equal(item.Category, decision.Category);
    }

    [Fact]
    public void ApproveNew_InvalidReplacement_PreservesPreviousApproval()
    {
        var review = CreateReview(1);
        var item = CreateNewItem();
        review.ApproveNew(0, item);

        Assert.Throws<InvalidOperationException>(() => review.ApproveNew(0, new WatchItem()));

        Assert.Equal(item.Id, review.GetDecision(0).ItemId);
        Assert.Equal(item.Id, Assert.Single(review.CreateFinalBatch().NewItems).Id);
    }

    [Fact]
    public void Skip_ApprovedNewRow_RemovesOnlyUnreferencedStagedItems()
    {
        var review = CreateReview(2);
        var item = CreateNewItem();
        review.ApproveNew(0, item);
        review.ApproveNew(1, CreateNewItem());

        review.Skip(0);

        Assert.Equal(MuseReviewDecisionStatus.Skipped, review.GetDecision(0).Status);
        Assert.Single(review.CreateFinalBatch().NewItems);
        review.Skip(1);
        Assert.Equal(0, review.GetSummary().NewItems);
        Assert.Throws<InvalidOperationException>(() => review.CreateFinalBatch());
    }

    [Fact]
    public void GetSummary_RepeatedIdentity_CountsUniqueItemsSeparatelyFromRows()
    {
        var review = CreateReview(4);
        review.ApproveNew(0, CreateNewItem());
        review.ApproveNew(1, CreateNewItem());
        review.ApproveExisting(2, new WatchItem { Title = "Saved film" });
        review.Skip(3);

        var summary = review.GetSummary();

        Assert.Equal(new MuseReviewSummary(4, 3, 1, 1, 1, 0, 3), summary);
        review.ClearApproval(3);
        Assert.Equal(MuseReviewDecisionStatus.Pending, review.GetDecision(3).Status);
        Assert.Equal(1, review.GetSummary().PendingRows);
    }

    [Fact]
    public void Skip_SourceLessRow_AllowsReviewToContinueWithoutImportingInvalidRow()
    {
        var row = new MuseRecommendation("Missing source", null, "note", "Films", [], 1);
        var review = new MuseImportReview([row]);

        review.Skip(0);

        Assert.Equal(1, review.GetSummary().SkippedRows);
        Assert.Equal(0, review.GetSummary().PendingRows);
    }

    private static MuseImportReview CreateReview(int count)
    {
        var rows = Enumerable.Range(0, count)
                             .Select(index => new MuseRecommendation($"Film {index}", null, "note", "Films", [$"https://example.com/{index}"], index + 1))
                             .ToList();
        return new MuseImportReview(rows);
    }

    private static WatchItem CreateNewItem()
    {
        return new WatchItem
               {
                   Title     = "Film"
                 , MovieId   = 123
                 , ApiSource = "TMDB"
                 , Category  = "Consider Watching"
                 , Type      = "Movie"
               };
    }

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
                          , Category  = "Consider Watching"
                        };
        var secondItem = new WatchItem
                         {
                             Title     = "Film"
                           , MovieId   = 123
                           , ApiSource = "tmdb"
                           , Category  = "Consider Watching"
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
                         , Category  = "Consider Watching"
                       };
        review.ApproveNew(0, proposed);
        review.ApproveExisting(1, new WatchItem { Title = "Second" });

        review.ClearApproval(0);
        var batch = review.CreateBatch();

        Assert.Empty(batch.NewItems);
        Assert.Single(batch.Recommendations);
    }

    [Fact]
    public void ApproveNew_MissingCategory_RequiresExplicitCategory()
    {
        var row = new MuseRecommendation("Film", null, "note", "Films", ["https://example.com/one"], 1);
        var review = new MuseImportReview([row]);
        var item = new WatchItem
                   {
                       Title     = "Film"
                     , MovieId   = 123
                     , ApiSource = "TMDB"
                   };

        Assert.Throws<InvalidOperationException>(() => review.ApproveNew(0, item));

        Assert.Equal(0, review.ApprovedCount);
    }
}
