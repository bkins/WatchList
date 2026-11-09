namespace WatchLists.Services.Restoration;

internal sealed class BackupColumn
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Pk { get; set; }
    public int Hidden { get; set; }
}
