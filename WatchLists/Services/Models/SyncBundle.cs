using WatchLists.MVVM.Models;

namespace WatchLists.Services.Models;

public class SyncBundle
{
    public DateTime        ExportedAtUtc     { get; set; } = DateTime.UtcNow;
    public string          DeviceId          { get; set; } = string.Empty;
    public List<WatchItem> Items             { get; set; } = new();
    public List<string>    Categories        { get; set; } = new();
    public List<string>    StreamingServices { get; set; } = new();
    public List<string>    Types             { get; set; } = new();
    public string          WatchedCategory   { get; set; } = string.Empty;
}
