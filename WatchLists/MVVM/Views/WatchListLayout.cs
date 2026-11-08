namespace WatchLists.MVVM.Views;

public static class WatchListLayout
{
    public static double GetHeaderMaximumHeight(double pageHeight)
    {
        if (!double.IsFinite(pageHeight) || pageHeight <= 0)
        {
            return 96;
        }

        return Math.Clamp(pageHeight * 0.4, 96, 320);
    }
}
