using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using WatchLists.Logger;
using WatchLists.Services;
using WatchLists.Utilities;

namespace WatchLists.MVVM.ViewModels;

public class LogsViewModel : BindableObject
{
    public           ObservableCollection<string> Logs { get; } = new();

    public ICommand ClearLogsCommand { get; }

    public LogsViewModel()
    {
        ClearLogsCommand = new Command(async () => await ClearLogs());
        LoadLogs();
    }

    public async void LoadLogs()
    {
        var fileContents = await FileLogger.ReadLogAsync();
        var lines = fileContents.Split(Environment.NewLine);

        foreach (var line in lines)
        {
            Logs.Add(line);
        }
    }

    private async Task ClearLogs()
    {
        if (File.Exists(LogConfig.LogFilePath))
        {
            File.Delete(LogConfig.LogFilePath);
        }

        // ✅ Recreate log file to ensure logging continues
        using (File.Create(LogConfig.LogFilePath))
        {
        }

        Logs.Clear();

        // ✅ Force logs to reload after clearing
        await FileLogger.WriteLogAsync("Log file cleared.");
        LoadLogs();

        System.Diagnostics.Debug.WriteLine($"Log file exists: {File.Exists(LogConfig.LogFilePath)}");

    }

}
