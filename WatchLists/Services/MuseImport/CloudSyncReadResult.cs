using WatchLists.Services.Models;

namespace WatchLists.Services.MuseImport;

public sealed record CloudSyncReadResult(SyncBundle? Bundle, string Etag);
