namespace WatchLists.Services.MuseImport;

public sealed class AsyncOperationGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<TResult> RunAsync<TResult>(Func<Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _semaphore.WaitAsync();
        try
        {
            return await operation();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
