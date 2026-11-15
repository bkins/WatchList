namespace WatchLists;

public static class WatchListVersion
{
    public static string DisplayVersion => typeof(WatchListVersion).Assembly.GetName().Version?.ToString(4) ?? "Unknown";
}
