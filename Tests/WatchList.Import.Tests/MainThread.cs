namespace WatchLists.MVVM.ViewModels;

internal static class MainThread
{
    public static void BeginInvokeOnMainThread(Action action) => action();
}
