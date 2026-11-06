namespace WatchLists.Services.MuseImport;

public static class SyncSafetyGate
{
    public const string PausedMessage = "Synchronization is temporarily unavailable while reliability work is completed. Turn off auto-sync to import recommendations.";

    public static Task<TResult> RunAsync<TResult>(Func<Task<TResult>> operation, TResult pausedResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return Task.FromResult(pausedResult);
    }
}
