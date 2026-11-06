using System.Text.Json;
using WatchLists.Services.Models;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class SyncSchemaGuardTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"SchemaVersion\":1}")]
    [InlineData("{\"SchemaVersion\":3}")]
    public void Validate_MissingOrIncompatibleVersion_RejectsBeforeMerge(string json)
    {
        var bundle = JsonSerializer.Deserialize<SyncBundle>(json)!;

        Assert.Throws<InvalidDataException>(() => SyncSchemaGuard.Validate(bundle.SchemaVersion));
    }

    [Fact]
    public void Validate_CurrentVersion_AcceptsPayload()
    {
        var bundle = JsonSerializer.Deserialize<SyncBundle>("{\"SchemaVersion\":2}")!;

        var exception = Record.Exception(() => SyncSchemaGuard.Validate(bundle.SchemaVersion));

        Assert.Null(exception);
    }
}
