namespace WatchLists.ExtensionMethods;

public static class StringExtensions
{
    public static bool IsEmpytNullOrWhiteSpace (this string value)
    {
        return string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace (value);
    }

    public static bool HasValue (this string value)
    {
        return ! IsEmpytNullOrWhiteSpace(value);
    }

    public static bool DoesNotContain (this List<string> value
                                     , string      substring)
    {
        return ! value.Contains (substring);
    }

    public static bool IsInt (this string value)
    {
        return int.TryParse(value, out _);
    }

    public static bool IsNotInt (this string value)
    {
        return ! IsInt (value);
    }
}
