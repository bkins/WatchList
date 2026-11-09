namespace WatchLists.MVVM.ViewModels;

internal sealed class Shell
{
    public static Shell Current { get; } = new();

    public Task GoToAsync(string route) => Task.CompletedTask;

    public Task DisplayAlertAsync(string title, string message, string cancel) =>
        throw new InvalidOperationException($"Unexpected {title} alert in an edit test: {message}");
}
