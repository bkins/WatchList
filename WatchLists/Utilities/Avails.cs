namespace WatchLists.Utilities;

public class Avails
{
    public static bool FileDoesNotExist (string filePath)
    {
        return ! File.Exists(filePath);
    }
}
