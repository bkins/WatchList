using System.Globalization;
using System.Text.RegularExpressions;

namespace WatchLists.Services.Models;

/// <summary>
/// Identifies a provider search result, never a saved recommendation occurrence.
/// Missing or conflicting identity evidence must leave results separate.
/// </summary>
public sealed record SearchResultIdentity(string Provider
                                        , string MediaType
                                        , string NativeId
                                        , string ReleaseYear)
{
    public static SearchResultIdentity? Create(string            providerName
                                             , MovieSearchResult result)
    {
        var mediaType = (result.MediaType ?? string.Empty).Trim().ToUpperInvariant();
        var nativeId  = string.Empty;

        // These providers expose native numeric IDs. Other adapters may use title/IMDb hashes.
        if (providerName == "TmdbService" && result.Id > 0)
        {
            mediaType = mediaType.Length == 0 ? "MOVIE" : mediaType;
            nativeId  = result.Id.ToString(CultureInfo.InvariantCulture);
        }
        else if (providerName == "TvMazeService" && result.Id > 0)
        {
            mediaType = mediaType.Length == 0 ? "SHOW" : mediaType;
            nativeId  = result.Id.ToString(CultureInfo.InvariantCulture);
        }
        else if (Uri.TryCreate(result.WebUrl, UriKind.Absolute, out var webUri)
              && webUri.Scheme is "https" or "http"
              && webUri.Host is "www.imdb.com" or "imdb.com")
        {
            var match = Regex.Match(webUri.AbsolutePath, @"^/title/(tt[0-9]+)/?$", RegexOptions.CultureInvariant);
            if (match.Success)
            {
                nativeId = match.Groups[1].Value;
            }
        }

        if (string.IsNullOrWhiteSpace(providerName) || nativeId.Length == 0)
        {
            return null;
        }

        var releaseDate = result.ReleaseDate ?? string.Empty;
        var yearMatch   = Regex.Match(releaseDate, @"^\s*([0-9]{4})(?:\D|$)", RegexOptions.CultureInvariant);
        var releaseYear = yearMatch.Success ? yearMatch.Groups[1].Value : releaseDate.Trim();

        return new SearchResultIdentity(providerName, mediaType, nativeId, releaseYear);
    }
}
