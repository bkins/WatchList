namespace WatchLists.Services.MuseImport;

public static class RecommendationSourceKey
{
    public static string Create(string sourceUrl)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("A valid HTTP or HTTPS source URL is required.", nameof(sourceUrl));
        }

        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        if (uri.Host.Equals("instagram.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".instagram.com", StringComparison.OrdinalIgnoreCase))
        {
            builder.Host  = "instagram.com";
            builder.Query = string.Empty;
            builder.Path  = builder.Path.TrimEnd('/');
        }

        return builder.Uri.AbsoluteUri;
    }
}
