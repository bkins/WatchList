using WatchLists.Services.Models;

namespace WatchLists.Services.Interfaces;

public interface IRemoteSyncProvider
{
    Task<SyncBundle?> FetchLatestBundleAsync (string endpointUrl, string syncCode);
    Task<bool> UploadBundleAsync (string endpointUrl, string syncCode, SyncBundle bundle);
    Task<string?> CreateNewCloudSyncBlobAsync ();
}
