namespace WatchLists.Utilities;

public static class LogConfig
{
    private static readonly string Directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder
                                                                                                 .LocalApplicationData));

    public static readonly string LogFilePath = Path.Combine(Directory, "app_logs.txt");

}
