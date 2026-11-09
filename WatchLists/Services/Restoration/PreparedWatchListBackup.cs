namespace WatchLists.Services.Restoration;

public sealed class PreparedWatchListBackup : IDisposable
{
    internal PreparedWatchListBackup(string path, string databasePath, string fingerprint, WatchListBackupSummary summary)
    {
        Path         = path;
        DatabasePath = databasePath;
        Fingerprint  = fingerprint;
        Summary      = summary;
    }

    internal string Path { get; }
    internal string DatabasePath { get; }
    internal string Fingerprint { get; }
    public WatchListBackupSummary Summary { get; }

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch (IOException)
        {
            // An abandoned staged copy can be reclaimed by the operating system.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
