using System.Text.Json;
using WatchLists.Services.Models;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class WatchProviderInfoTests
{
    [Fact]
    public void Deserialize_OmittedProviderLists_CanEnumerateAllAvailabilityTypes()
    {
        var result = JsonSerializer.Deserialize<WatchProviderInfo>("{}");

        Assert.NotNull(result);
        Assert.Empty(result.Flatrate.Concat(result.Rent).Concat(result.Buy));
    }
}
