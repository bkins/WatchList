using WatchLists.Services.Models;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class CloudSyncUploadCoordinatorTests
{
    [Fact]
    public async Task UploadAsync_UnreadableRemote_DoesNotUpload()
    {
        var uploaded = false;
        var coordinator = new CloudSyncUploadCoordinator();
        var bundle = new SyncBundle { SchemaVersion = 2 };

        var result = await coordinator.UploadAsync(bundle, () => Task.FromResult<SyncBundle?>(null)
                                                 , remote => Task.CompletedTask, () => Task.FromResult(bundle)
                                                 , current => { uploaded = true; return Task.FromResult(true); }, false);

        Assert.False(result);
        Assert.False(uploaded);
    }

    [Fact]
    public async Task UploadAsync_LegacyRemote_RejectsBeforeUpload()
    {
        var uploaded = false;
        var coordinator = new CloudSyncUploadCoordinator();
        var bundle = new SyncBundle { SchemaVersion = 2 };
        var legacy = new SyncBundle();

        await Assert.ThrowsAsync<InvalidDataException>(() => coordinator.UploadAsync(bundle, () => Task.FromResult<SyncBundle?>(legacy)
            , remote => Task.CompletedTask, () => Task.FromResult(bundle)
            , current => { uploaded = true; return Task.FromResult(true); }, false));

        Assert.False(uploaded);
    }

    [Fact]
    public async Task UploadAsync_CurrentRemote_MergesBeforeTakingUploadSnapshot()
    {
        var coordinator = new CloudSyncUploadCoordinator();
        var initial = new SyncBundle { SchemaVersion = 2 };
        var remote = new SyncBundle { SchemaVersion = 2 };
        var merged = false;
        var uploadedMerged = false;

        var result = await coordinator.UploadAsync(initial, () => Task.FromResult<SyncBundle?>(remote)
            , value => { merged = true; return Task.CompletedTask; }
            , () => Task.FromResult(new SyncBundle { SchemaVersion = merged ? 2 : 0 })
            , value => { uploadedMerged = value.SchemaVersion == 2 && merged; return Task.FromResult(true); }, false);

        Assert.True(result);
        Assert.True(uploadedMerged);
    }
}
