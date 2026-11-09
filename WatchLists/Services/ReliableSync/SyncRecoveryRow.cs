using SQLite;

namespace WatchLists.Services.ReliableSync;

[Table("ReliableSyncRecovery")]
internal sealed class SyncRecoveryRow
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}
