namespace WatchLists.Utilities;

public static class LogConfig
{
    private static readonly string Directory = FileSystem.AppDataDirectory;

    public static readonly string LogFilePath = Path.Combine(Directory, "app_logs.txt");

}
