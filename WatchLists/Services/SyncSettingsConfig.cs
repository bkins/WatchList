namespace WatchLists.Services;

public class SyncSettingsConfig
{
    public string SyncMode        { get; set; } = "CloudApi";
    public string SyncFolderPath  { get; set; } = string.Empty;
    public string ApiEndpointUrl  { get; set; } = "https://watchlist-faa16-default-rtdb.firebaseio.com/";
    public string SyncCode        { get; set; } = "MyWatchList2026";
    public bool   AutoSyncEnabled { get; set; } = true;
}
