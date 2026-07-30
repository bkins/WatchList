namespace WatchLists.Utilities;

public static class LogConfig
{
    private static readonly string LogDir;

    static LogConfig()
    {
        try
        {
            LogDir = FileSystem.AppDataDirectory;
        }
        catch
        {
            LogDir = Path.Combine(Path.GetTempPath(), "WatchList");
            Directory.CreateDirectory(LogDir);
        }
    }

    public static string LogFilePath => Path.Combine(LogDir, "app_logs.txt");
}
