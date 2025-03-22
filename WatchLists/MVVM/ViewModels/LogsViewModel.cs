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

    private async void LoadLogs()
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

        Logs.Clear();
    }
}
