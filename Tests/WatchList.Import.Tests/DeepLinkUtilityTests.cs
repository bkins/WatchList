using WatchLists.Utilities;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class DeepLinkUtilityTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GenerateDeepLink_MissingWebUrl_UsesProviderSearch(string? webUrl)
    {
        var result = DeepLinkUtility.GenerateDeepLink("Netflix", "Synthetic Film", webUrl);

        Assert.Equal("https://www.netflix.com/search?q=Synthetic%20Film", result);
    }

    [Fact]
    public void GenerateDeepLink_RecognizedWebUrl_RetainsTitleIdentity()
    {
        var result = DeepLinkUtility.GenerateDeepLink("Netflix", "Synthetic", "https://www.netflix.com/title/123");

        Assert.Equal("netflix://title/123", result);
    }

    [Fact]
    public void GenerateDeepLink_UnknownProvider_PreservesSuppliedUrl()
    {
        var result = DeepLinkUtility.GenerateDeepLink("Other", "Synthetic", "https://example.test/film");

        Assert.Equal("https://example.test/film", result);
    }
}
