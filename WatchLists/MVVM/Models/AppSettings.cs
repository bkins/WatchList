namespace WatchLists.MVVM.Models;

public class AppSettings
{
    private const string WatchedCategoryKey = "WatchedCategory";

    public static string WatchedCategory
    {
        get => Preferences.Get(WatchedCategoryKey
                             , "Watched"); // Default to "Watched"
        set => Preferences.Set(WatchedCategoryKey
                             , value);
    }
}
