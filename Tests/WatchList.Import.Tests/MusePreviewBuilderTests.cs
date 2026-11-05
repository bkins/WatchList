using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MusePreviewBuilderTests
{
    [Fact]
    public void Build_TitleMatches_OffersActiveAndDeletedItemsForReview()
    {
        var builder = new MusePreviewBuilder();
        var recommendation = new MuseRecommendation("Alone", 2020, "thriller", "Films", [], 1);
        var items = new[]
                    {
                        new MuseExistingItem(Guid.NewGuid(), "alone", false, 1, "TMDB")
                      , new MuseExistingItem(Guid.NewGuid(), "Alone", true, 2, "TMDB")
                      , new MuseExistingItem(Guid.NewGuid(), "Other", false, 3, "TMDB")
                    };

        var result = builder.Build([recommendation], items);

        var preview = Assert.Single(result);
        Assert.Equal(2, preview.PossibleMatches.Count);
        Assert.Contains(preview.PossibleMatches, item => item.IsDeleted);
    }
}
