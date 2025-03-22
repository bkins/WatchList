using WatchLists.Utilities;

namespace WatchLists.Logger;

public static class FileLogger
{
    // Use the same path as before, for consistency with your other text data.
    public static readonly string LogFilePath = LogConfig.LogFilePath;

    static FileLogger()
    {
        //ClearLog();
        //_ = WriteLogAsync("FileLogger initialized");
    }
    /// <summary>
    /// Appends a log entry to the log file.
    /// </summary>
    public static async Task WriteLogAsync (string message)
    {
        try
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(LogFilePath
                                        , logEntry);
        }
        catch (Exception ex)
        {
            // Handle exceptions (maybe write to a secondary log or debug output)
            System.Diagnostics.Debug.WriteLine($"Error writing log: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads the log file content.
    /// </summary>
    public static async Task<string> ReadLogAsync()
    {
        try
        {
            if (File.Exists(LogFilePath))
            {
                return await File.ReadAllTextAsync(LogFilePath);
            }

            return "Log file does not exist.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading log: {ex.Message}");

            return $"Error reading log: {ex.Message}";
        }
    }

    /// <summary>
    /// Clears the log file.
    /// </summary>
    public static void ClearLog()
    {
        try
        {
            if (File.Exists(LogFilePath))
            {
                File.WriteAllText(LogFilePath
                                , string.Empty);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing log: {ex.Message}");
        }
    }
}
