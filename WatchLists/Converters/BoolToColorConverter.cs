using System.Globalization;

namespace WatchLists.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object      value
                        , Type        targetType
                        , object      parameter
                        , CultureInfo culture)
    {
        return value is true
            ? Colors.Green
            : Colors.Transparent;
    }

    public object ConvertBack(object      value
                            , Type        targetType
                            , object      parameter
                            , CultureInfo culture)
    {
        return null;
    }
}
