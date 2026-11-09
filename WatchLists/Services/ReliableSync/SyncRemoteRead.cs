namespace WatchLists.Services.ReliableSync;

public sealed record SyncRemoteRead(SyncEnvelope? Envelope, string Etag);
