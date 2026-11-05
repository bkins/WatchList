using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseMarkdownParserTests
{
    [Fact]
    public void Parse_RecommendationWithYearAndMultipleSources_PreservesDetails()
    {
        var parser = new MuseMarkdownParser();
        var markdown = "## Films\n- **Alone** (2020) — A thriller\n  Source: [one](https://example.com/one), [two](https://example.com/two)";

        var result = parser.Parse(markdown);

        var item = Assert.Single(result.Recommendations);
        Assert.Equal("Alone", item.Title);
        Assert.Equal(2020, item.ReleaseYear);
        Assert.Equal("A thriller", item.Description);
        Assert.Equal("Films", item.Section);
        Assert.Equal(2, item.SourceUrls.Count);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Parse_MalformedAndMissingSource_ReportsWarningsWithoutDiscardingValidItem()
    {
        var parser = new MuseMarkdownParser();
        var markdown = "## Films\n- **Broken\n- **Valid** — description";

        var result = parser.Parse(markdown);

        Assert.Single(result.Recommendations);
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public void Parse_EmptyDocument_ReportsNoRecommendations()
    {
        var parser = new MuseMarkdownParser();

        var result = parser.Parse(string.Empty);

        Assert.Empty(result.Recommendations);
        Assert.Single(result.Warnings);
    }
}
