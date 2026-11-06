using WatchLists.Services.Models;
using WatchLists.Services.MuseImport;

namespace WatchLists.Services.Interfaces;

public interface IRemoteSyncProvider
{
    Task<SyncBundle?> FetchLatestBundleAsync (string endpointUrl, string syncCode);
    Task<CloudSyncReadResult?> FetchConditionalAsync (string endpointUrl, string syncCode);
    Task<CloudSyncWriteResult> UploadConditionalAsync (string endpointUrl, string syncCode, SyncBundle bundle, string etag);
}
