namespace WatchLists.Services.ReliableSync;

public sealed record SyncConflict(Guid         Id
                                , SyncSnapshot Baseline
                                , SyncSnapshot Local
                                , SyncSnapshot Remote
                                , string?      RemoteRevision
                                , string       RemoteEtag
                                , Guid[]       ItemIds);
