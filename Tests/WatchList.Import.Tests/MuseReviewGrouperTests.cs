using SQLite;
using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseReviewGrouperTests
{
    [Fact]
    public void Group_SummarySourceSubset_PreservesEveryOriginalAndPrimaryOrder()
    {
        var summary = CreateRow("Recommended more than once", 1);
        var first = CreateRow("Films", 5) with { Title = "Other" };
        var primary = CreateRow("Films", 9) with { SourceUrls = ["https://example.test/one", "https://example.test/two"] };
        var rows = new[] { summary, first, primary, summary with { LineNumber = 12 } };

        var groups = new MuseReviewGrouper().Group(rows);

        Assert.Equal(2, groups.Count);
        Assert.Same(first, groups[0].Representative);
        Assert.Same(primary, groups[1].Representative);
        Assert.Equal(new[] { rows[0], rows[2], rows[3] }, groups[1].Members);
        Assert.Equal("3 document entries grouped", groups[1].GroupingLabel);
        Assert.False(groups[0].IsGrouped);
        Assert.Empty(groups[0].GroupingLabel);
        Assert.Equal(rows.Length, groups.Sum(group => group.Members.Count));
        Assert.Equal(groups.Select(group => group.Representative), new MuseReviewGrouper().Group(rows).Select(group => group.Representative));
    }

    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("year")]
    [InlineData("missing-year")]
    [InlineData("summary-sources")]
    [InlineData("primary-sources")]
    [InlineData("extra-source")]
    [InlineData("heading")]
    [InlineData("url-case")]
    public void Group_DifferingOrUnsupportedContent_LeavesSeparateItems(string difference)
    {
        var primary = CreateRow("Films", 5);
        var summary = CreateRow("Recommended more than once", 1);
        switch (difference)
        {
            case "title":
                summary = summary with { Title = "film" };
                break;
            case "description":
                summary = summary with { Description = "Different" };
                break;
            case "year":
                summary = summary with { ReleaseYear = 2021 };
                break;
            case "missing-year":
                summary = summary with { ReleaseYear = null };
                break;
            case "summary-sources":
                summary = summary with { SourceUrls = [] };
                break;
            case "primary-sources":
                primary = primary with { SourceUrls = [] };
                break;
            case "extra-source":
                summary = summary with { SourceUrls = ["https://example.test/one", "https://example.test/extra"] };
                break;
            case "heading":
                summary = summary with { Section = "Other summary" };
                break;
            case "url-case":
                summary = summary with { SourceUrls = ["https://example.test/One"] };
                break;
        }

        var groups = new MuseReviewGrouper().Group([summary, primary]);

        Assert.Equal(2, groups.Count);
        Assert.All(groups, group => Assert.False(group.IsGrouped));
    }

    [Fact]
    public void Group_AmbiguousFilmAndTelevisionCounterparts_DoesNotChooseIdentity()
    {
        var summary = CreateRow("Recommended more than once", 1);
        var film = CreateRow("Films", 5);
        var television = CreateRow("TV series", 9) with { SourceUrls = ["https://example.test/other"] };

        var groups = new MuseReviewGrouper().Group([summary, film, television]);

        Assert.Equal(3, groups.Count);
        Assert.All(groups, group => Assert.False(group.IsGrouped));
    }

    [Fact]
    public void Group_SourcesCoveredOnlyAcrossMultipleRows_DoesNotCombineCounterparts()
    {
        var summary = CreateRow("Recommended more than once", 1) with { SourceUrls = ["https://example.test/one", "https://example.test/two"] };
        var first = CreateRow("Films", 5);
        var second = CreateRow("Films", 9) with { SourceUrls = ["https://example.test/two"] };

        var groups = new MuseReviewGrouper().Group([summary, first, second]);

        Assert.Equal(3, groups.Count);
    }

    [Fact]
    public void Group_MissingYearsInBothRows_GroupsWithoutInferringYear()
    {
        var summary = CreateRow("Recommended more than once", 1) with { ReleaseYear = null };
        var primary = CreateRow("Films", 5) with { ReleaseYear = null };

        var group = Assert.Single(new MuseReviewGrouper().Group([summary, primary]));

        Assert.Null(group.Representative.ReleaseYear);
        Assert.Equal(2, group.Members.Count);
    }

    [Fact]
    public void Group_EmptyInput_ReturnsNoGroups()
    {
        var groups = new MuseReviewGrouper().Group([]);

        Assert.Empty(groups);
        Assert.Throws<ArgumentNullException>(() => new MuseReviewGrouper().Group(null!));
    }

    [Fact]
    public async Task Group_ReviewedSession_UsesOneDecisionAndPreservesAllSourceAssociations()
    {
        var summary = CreateRow("Recommended more than once", 1);
        var primary = CreateRow("Films", 5) with { SourceUrls = ["https://example.test/one", "https://example.test/two"] };
        var other = CreateRow("Films", 9) with { Title = "Other" };
        var groups = new MuseReviewGrouper().Group([summary, primary, other]);
        var session = new MuseSuggestionSession(groups.Select(group => group.Representative).ToList());
        var item = new WatchItem { Title = "Selected identity" };
        var searches = new List<string>();

        await session.SearchPendingAsync((title, cancellation) =>
        {
            searches.Add(title);
            return Task.FromResult<IReadOnlyList<MuseProviderCandidate>>([]);
        }, result => { });
        session.Review.ApproveExisting(0, item);
        Assert.Throws<InvalidOperationException>(() => session.Review.CreateFinalBatch());
        session.Review.ClearApproval(0);
        Assert.Equal(2, session.Review.GetSummary().PendingRows);
        session.Review.ApproveExisting(0, item);
        session.Review.SkipPending();
        var batch = session.Review.CreateFinalBatch();

        Assert.Equal(2, searches.Count);
        Assert.Equal(1, session.Review.ApprovedCount);
        Assert.Equal(1, session.Review.GetSummary().SkippedRows);
        Assert.Equal(primary.SourceUrls, batch.Recommendations.Select(entry => entry.SourceUrl));
        Assert.All(batch.Recommendations, entry => Assert.Equal(primary.Description, entry.Description));
        session.ResetApprovals();
        Assert.Equal(2, session.Review.GetSummary().PendingRows);
    }

    [Fact]
    public void Group_ApprovedAndRepeatedImport_PreservesSourcesAndExistingPersonalFields()
    {
        SQLitePCL.Batteries_V2.Init();
        var directory = Path.Combine(Path.GetTempPath(), "WatchListImportTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var connection = new SQLiteConnection(Path.Combine(directory, "test.db"));
            connection.CreateTable<WatchItem>();
            var item = new WatchItem
                       {
                           Title     = "Existing identity"
                         , IsWatched = true
                         , IsLiked   = true
                         , Category  = "Finished Watching"
                       };
            connection.Insert(item);
            var summary = CreateRow("Recommended more than once", 1);
            var primary = CreateRow("Films", 5) with { SourceUrls = ["https://example.test/one", "https://example.test/two"] };
            var group = Assert.Single(new MuseReviewGrouper().Group([summary, primary]));
            var review = new MuseImportReview([group.Representative]);
            review.ApproveExisting(0, item);
            var batch = review.CreateFinalBatch();
            var writer = new MuseImportWriter(connection);

            var first = writer.Apply(batch, Path.Combine(directory, "before-first.db"), DateTime.UtcNow);
            var repeated = writer.Apply(batch, Path.Combine(directory, "before-repeat.db"), DateTime.UtcNow);

            Assert.Equal(2, first.NewRecommendations);
            Assert.Equal(0, repeated.NewItems);
            Assert.Equal(0, repeated.NewRecommendations);
            Assert.Equal(2, new RecommendationStore(connection).GetAll().Count);
            var saved = Assert.Single(connection.Table<WatchItem>().ToList());
            Assert.True(saved.IsWatched);
            Assert.True(saved.IsLiked);
            Assert.Equal(item.Category, saved.Category);
            Assert.Equal(item.Title, saved.Title);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
    private static MuseRecommendation CreateRow(string section, int line)
    {
        return new MuseRecommendation("Film", 1982, "A description", section, ["https://example.test/one"], line);
    }
}
