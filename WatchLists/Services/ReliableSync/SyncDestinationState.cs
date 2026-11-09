namespace WatchLists.Services.ReliableSync;

internal sealed record SyncDestinationState(SyncSnapshot Baseline
                                           , string?      Revision
                                           , SyncPending? Pending
                                           , SyncConflict? Conflict)
{
    public static SyncDestinationState New { get; } = new(SyncSnapshot.Empty, null, null, null);
}
