namespace WatchLists.Services.ReliableSync;

public sealed record SyncSnapshot(SyncItem[] Items, SyncLink[] Links)
{
    public static SyncSnapshot Empty { get; } = new([], []);

    public SyncSnapshot Ordered()
    {
        return new SyncSnapshot(Items.OrderBy(item => item.Id).ToArray()
                              , Links.OrderBy(link => link.WatchItemId)
                                     .ThenBy(link => link.SourceKey, StringComparer.Ordinal)
                                     .ToArray());
    }
}
