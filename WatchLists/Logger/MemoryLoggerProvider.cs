using Microsoft.Extensions.Logging;

namespace WatchLists.Logger;

public class MemoryLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger (string categoryName) => new MemoryLogger(categoryName);

    public void Dispose()
    {
    }
}
