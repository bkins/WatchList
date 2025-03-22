using System.Globalization;
using WatchLists.ExtensionMethods;

namespace WatchLists.Converters;

public class ImageConverter : IValueConverter
{
    private const string BaseImageUrl = "https://image.tmdb.org/t/p/w500";

    public object Convert(object      value
                        , Type        targetType
                        , object      parameter
                        , CultureInfo culture)
    {
        if (value is string imagePath
         && imagePath.HasValue())
        {
            return new Uri($"{BaseImageUrl}{imagePath}");
        }

        return "placeholder.png";
    }

    public object ConvertBack(object      value
                            , Type        targetType
                            , object      parameter
                            , CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
