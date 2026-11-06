namespace WatchLists.Services;

internal static class FileSystem
{
    public static string AppDataDirectory => throw new InvalidOperationException("Tests must supply an isolated settings folder.");
}
