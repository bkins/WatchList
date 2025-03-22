

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace WatchLists.Logger;

public class MemoryLogger : ILogger
{
    private readonly string                       _categoryName;
    private static   ObservableCollection<string> _logs = new ObservableCollection<string>();

    public static ObservableCollection<string> Logs => _logs;

    public bool IsEnabled (LogLevel logLevel) => true;

    public MemoryLogger (string categoryName)
    {
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState> (TState state) => null;

    /// <inheritdoc />
    public void Log<TState> (LogLevel                         logLevel
                           , EventId                          eventId
                           , TState                           state
                           , Exception?                       exception
                           , Func<TState, Exception?, string> formatter)
    {
        var message = $"[{DateTime.Now:HH:mm:ss}] {logLevel}: {formatter(state, exception)}";

        if (exception != null)
        {
            message += $"\nException: {exception}";
        }

        _logs.Add(message);
    }


}
