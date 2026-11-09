using SQLite;

namespace WatchLists.Services.ReliableSync;

[Table("ReliableSyncState")]
internal sealed class SyncStateRow
{
    [PrimaryKey]
    public string Destination { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}
