using System.Globalization;

namespace WatchLists.Converters;

public class ToggledEventArgsConverter : IValueConverter
{
    public object Convert (object      value
                         , Type        targetType
                         , object      parameter
                         , CultureInfo culture)
    {
        // The value is expected to be a ToggledEventArgs instance.
        if (value is ToggledEventArgs args)
        {
            return args.Value; // Return the boolean value from the event.
        }

        return false;
    }

    public object ConvertBack (object      value
                             , Type        targetType
                             , object      parameter
                             , CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
