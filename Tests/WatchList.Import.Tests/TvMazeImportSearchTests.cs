using System.Text.Json;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class TvMazeImportSearchTests
{
    [Fact]
    public void Parse_ShowsWithSameName_KeepsBothIdentitiesAndDates()
    {
        using var document = JsonDocument.Parse("""
            [{"show":{"id":1,"name":"The Show","premiered":"2001-01-01","summary":"<p>First &amp; best</p>"}},
             {"show":{"id":2,"name":"The Show","premiered":"2025-05-05","summary":null}},
             {"show":{"id":"bad","name":"Invalid"}}]
            """);

        var results = TvMazeImportSearch.Parse(document.RootElement);

        Assert.Equal(2, results.Count);
        Assert.Equal("2001-01-01", results[0].Date);
        Assert.Equal("First & best", results[0].Overview);
        Assert.Equal("TVMaze", results[1].Provider);
        Assert.Equal("Show", results[1].MediaType);
        Assert.NotEqual(results[0].Id, results[1].Id);
    }

    [Fact]
    public void Parse_NonArray_ReportsInvalidResponse()
    {
        using var document = JsonDocument.Parse("{}");

        Assert.Throws<InvalidDataException>(() => TvMazeImportSearch.Parse(document.RootElement));
    }

    [Fact]
    public async Task SearchAsync_TitleWithSpecialCharacters_EncodesQuery()
    {
        var handler = new RecordingHttpHandler();
        using var client = new HttpClient(handler);
        var search = new TvMazeImportSearch(client);

        var result = await search.SearchAsync("Black & White");

        Assert.Empty(result);
        Assert.NotNull(handler.RequestedUri);
        Assert.Equal("?q=Black%20%26%20White", handler.RequestedUri.Query);
    }
}
