using System;
using System.Text.RegularExpressions;

namespace WatchLists.Utilities;

public static class DeepLinkUtility
{
    public static string GenerateDeepLink(string providerName, string title, string? webUrl = null)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return webUrl ?? string.Empty;
        }

        var normalizedProvider = providerName.ToLowerInvariant().Trim();

        // 1. If we have a direct web URL, try to convert it to a deep link URI
        if (!string.IsNullOrWhiteSpace(webUrl))
        {
            if (normalizedProvider.Contains("netflix"))
            {
                var match = Regex.Match(webUrl, @"netflix\.com/title/(\d+)");
                if (match.Success)
                {
                    return $"netflix://title/{match.Groups[1].Value}";
                }
            }
            else if (normalizedProvider.Contains("prime video") || normalizedProvider.Contains("amazon"))
            {
                var match = Regex.Match(webUrl, @"amazon\.com/gp/video/detail/([^/?#]+)");
                if (match.Success)
                {
                    return $"primevideo://watch?gti={match.Groups[1].Value}";
                }
            }
            else if (normalizedProvider.Contains("disney"))
            {
                var match = Regex.Match(webUrl, @"disneyplus\.com/(?:movies|video)/[^/]+/([^/?#]+)");
                if (match.Success)
                {
                    return $"disneyplus://play/{match.Groups[1].Value}";
                }
            }
            else if (normalizedProvider.Contains("hulu"))
            {
                var match = Regex.Match(webUrl, @"hulu\.com/watch/([^/?#]+)");
                if (match.Success)
                {
                    return $"hulu://w/{match.Groups[1].Value}";
                }
            }
            else if (normalizedProvider.Contains("max") || normalizedProvider.Contains("hbo"))
            {
                var match = Regex.Match(webUrl, @"max\.com/(?:video/watch|show)/([^/?#]+)");
                if (match.Success)
                {
                    return $"max://play/{match.Groups[1].Value}";
                }
            }
            else if (normalizedProvider.Contains("youtube"))
            {
                var match = Regex.Match(webUrl, @"youtube\.com/watch\?v=([^&]+)");
                if (match.Success)
                {
                    return $"youtube://watch?v={match.Groups[1].Value}";
                }
            }

            return webUrl;
        }

        // 2. Search-based deep link fallbacks
        var escapedTitle = Uri.EscapeDataString(title);
        if (normalizedProvider.Contains("netflix"))
        {
            return $"netflix://search?q={escapedTitle}";
        }
        if (normalizedProvider.Contains("prime video") || normalizedProvider.Contains("amazon"))
        {
            return $"primevideo://search?phrase={escapedTitle}";
        }
        if (normalizedProvider.Contains("disney"))
        {
            return "disneyplus://";
        }
        if (normalizedProvider.Contains("hulu"))
        {
            return "hulu://";
        }
        if (normalizedProvider.Contains("max") || normalizedProvider.Contains("hbo"))
        {
            return "max://";
        }
        if (normalizedProvider.Contains("youtube"))
        {
            return $"youtube://results?search_query={escapedTitle}";
        }

        return string.Empty;
    }
}
