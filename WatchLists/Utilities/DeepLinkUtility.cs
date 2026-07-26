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
            else if (normalizedProvider.Contains("apple"))
            {
                var match = Regex.Match(webUrl, @"tv\.apple\.com/[^/]+/(?:show|movie)/[^/]+/([^/?#]+)");
                if (match.Success)
                {
                    return webUrl;
                }
            }

            return webUrl;
        }

        // 2. Search-based deep link fallbacks
        var escapedTitle = Uri.EscapeDataString(title);
        if (normalizedProvider.Contains("apple"))
        {
            return $"https://tv.apple.com/us/search?term={escapedTitle}";
        }
        if (normalizedProvider.Contains("netflix"))
        {
            return $"https://www.netflix.com/search?q={escapedTitle}";
        }
        if (normalizedProvider.Contains("prime video") || normalizedProvider.Contains("amazon"))
        {
            return $"https://www.amazon.com/s?k={escapedTitle}&i=instant-video";
        }
        if (normalizedProvider.Contains("disney"))
        {
            return $"https://www.disneyplus.com/search?q={escapedTitle}";
        }
        if (normalizedProvider.Contains("hulu"))
        {
            return $"https://www.hulu.com/search?q={escapedTitle}";
        }
        if (normalizedProvider.Contains("max") || normalizedProvider.Contains("hbo"))
        {
            return $"https://www.max.com/search?q={escapedTitle}";
        }
        if (normalizedProvider.Contains("youtube"))
        {
            return $"https://www.youtube.com/results?search_query={escapedTitle}";
        }
        if (normalizedProvider.Contains("paramount"))
        {
            return $"https://www.paramountplus.com/search/?q={escapedTitle}";
        }
        if (normalizedProvider.Contains("peacock"))
        {
            return $"https://www.peacocktv.com/watch/search?q={escapedTitle}";
        }

        return !string.IsNullOrWhiteSpace(webUrl) ? webUrl : $"https://www.google.com/search?q={escapedTitle}+watch+online";
    }
}
