using WatchLists.Services.Models;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class CloudSyncUploadCoordinatorTests
{
    [Fact]
    public async Task UploadAsync_UnreadableRemote_DoesNotUpload()
    {
        var uploads = 0;
        var coordinator = new CloudSyncUploadCoordinator();

        var result = await coordinator.UploadAsync(() => Task.FromResult<CloudSyncReadResult?>(null)
            , remote => Task.CompletedTask
            , () => Task.FromResult(new SyncBundle())
            , (bundle, etag) => { uploads++; return Task.FromResult(CloudSyncWriteResult.Success); }, false);

        Assert.False(result);
        Assert.Equal(0, uploads);
    }

    [Fact]
    public async Task UploadAsync_LegacyRemote_RejectsBeforeUpload()
    {
        var uploads = 0;
        var coordinator = new CloudSyncUploadCoordinator();

        await Assert.ThrowsAsync<InvalidDataException>(() => coordinator.UploadAsync(
            () => Task.FromResult<CloudSyncReadResult?>(new(new SyncBundle(), "revision"))
            , remote => Task.CompletedTask
            , () => Task.FromResult(new SyncBundle())
            , (bundle, etag) => { uploads++; return Task.FromResult(CloudSyncWriteResult.Success); }, false));

        Assert.Equal(0, uploads);
    }

    [Fact]
    public async Task UploadAsync_Conflict_MergesLatestBeforeRetry()
    {
        var reads = 0;
        var localCategories = new HashSet<string> { "local" };
        var coordinator = new CloudSyncUploadCoordinator();
        SyncBundle? uploaded = null;
        var tags = new List<string>();

        var result = await coordinator.UploadAsync(
            () => Task.FromResult<CloudSyncReadResult?>(new(new SyncBundle
                { SchemaVersion = 2, Categories = [++reads == 1 ? "first" : "second"] }, $"revision-{reads}"))
            , remote => { localCategories.UnionWith(remote.Categories); return Task.CompletedTask; }
            , () => Task.FromResult(new SyncBundle { SchemaVersion = 2, Categories = localCategories.ToList() })
            , (bundle, etag) => { uploaded = bundle; tags.Add(etag); return Task.FromResult(tags.Count == 1 ? CloudSyncWriteResult.Conflict : CloudSyncWriteResult.Success); }
            , false);

        Assert.True(result);
        Assert.Equal(new[] { "revision-1", "revision-2" }, tags);
        Assert.Contains("local", uploaded!.Categories);
        Assert.Contains("first", uploaded.Categories);
        Assert.Contains("second", uploaded.Categories);
    }

    [Fact]
    public async Task UploadAsync_RepeatedConflicts_StopsAfterThreeAttempts()
    {
        var uploads = 0;
        var coordinator = new CloudSyncUploadCoordinator();

        var result = await coordinator.UploadAsync(() => Task.FromResult<CloudSyncReadResult?>(new(new SyncBundle { SchemaVersion = 2 }, "revision"))
            , remote => Task.CompletedTask
            , () => Task.FromResult(new SyncBundle { SchemaVersion = 2 })
            , (bundle, etag) => { uploads++; return Task.FromResult(CloudSyncWriteResult.Conflict); }, false);

        Assert.False(result);
        Assert.Equal(3, uploads);
    }

    [Fact]
    public async Task UploadAsync_NewDestination_BlocksExistingOrMissingRevision()
    {
        var uploads = 0;
        var coordinator = new CloudSyncUploadCoordinator();
        var result = await coordinator.UploadAsync(() => Task.FromResult<CloudSyncReadResult?>(new(new SyncBundle { SchemaVersion = 2 }, "revision"))
            , remote => Task.CompletedTask
            , () => Task.FromResult(new SyncBundle())
            , (bundle, etag) => { uploads++; return Task.FromResult(CloudSyncWriteResult.Success); }, true);

        Assert.False(result);
        Assert.Equal(0, uploads);
    }
    [Fact]
    public async Task UploadAsync_EmptyNewDestination_UsesObservedRevision()
    {
        string? usedRevision = null;
        var coordinator = new CloudSyncUploadCoordinator();

        var result = await coordinator.UploadAsync(() => Task.FromResult<CloudSyncReadResult?>(new(null, "null_etag"))
            , remote => throw new InvalidOperationException("Empty destinations must not be merged.")
            , () => Task.FromResult(new SyncBundle { SchemaVersion = 2 })
            , (bundle, etag) => { usedRevision = etag; return Task.FromResult(CloudSyncWriteResult.Success); }, true);

        Assert.True(result);
        Assert.Equal("null_etag", usedRevision);
    }
}
