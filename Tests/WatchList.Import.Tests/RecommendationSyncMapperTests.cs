using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class RecommendationSyncMapperTests
{
    [Fact]
    public void Map_DifferentIdsWithSameProviderIdentity_MapsToLocalItem()
    {
        var incoming = new WatchItem
                       {
                           MovieId   = 42
                         , ApiSource = "TMDB"
                       };
        var local = new WatchItem
                    {
                        MovieId   = 42
                      , ApiSource = "tmdb"
                    };
        var recommendation = new MovieRecommendation { WatchItemId = incoming.Id };

        var mapping = new RecommendationSyncMapper().Map([incoming], [local], [recommendation]);

        Assert.Equal(local.Id, mapping[incoming.Id]);
    }

    [Fact]
    public void Map_SameTitleWithoutProviderIdentity_RejectsUnsafeMatch()
    {
        var incoming = new WatchItem { Title = "Same" };
        var local = new WatchItem { Title = "Same" };
        var recommendation = new MovieRecommendation { WatchItemId = incoming.Id };

        Assert.Throws<InvalidOperationException>(() => new RecommendationSyncMapper().Map([incoming], [local], [recommendation]));
    }

    [Fact]
    public void Map_AmbiguousProviderIdentity_RejectsUnsafeMatch()
    {
        var incoming = new WatchItem
                       {
                           MovieId   = 42
                         , ApiSource = "TMDB"
                       };
        var first = new WatchItem
                    {
                        MovieId   = 42
                      , ApiSource = "TMDB"
                    };
        var second = new WatchItem
                     {
                         MovieId   = 42
                       , ApiSource = "tmdb"
                     };
        var recommendation = new MovieRecommendation { WatchItemId = incoming.Id };

        Assert.Throws<InvalidOperationException>(() => new RecommendationSyncMapper().Map([incoming], [first, second], [recommendation]));
    }
}
