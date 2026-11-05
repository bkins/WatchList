using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WatchLists.Services.MuseImport;

public sealed class TvMazeImportSearch
{
    private readonly HttpClient _httpClient;

    public TvMazeImportSearch(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<IReadOnlyList<MuseProviderCandidate>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("A title is required.", nameof(query));
        var address = "https://api.tvmaze.com/search/shows?q=" + Uri.EscapeDataString(query.Trim());
        using var response = await _httpClient.GetAsync(address, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        return Parse(document.RootElement);
    }

    public static IReadOnlyList<MuseProviderCandidate> Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) throw new InvalidDataException("TVMaze search response is not a list.");
        var candidates = new List<MuseProviderCandidate>();
        foreach (var result in root.EnumerateArray())
        {
            if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("show", out var show) ||
                show.ValueKind != JsonValueKind.Object || !show.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number ||
                !id.TryGetInt32(out var value) || value <= 0) continue;

            var title = GetString(show, "name");
            if (string.IsNullOrWhiteSpace(title)) continue;
            var summary = WebUtility.HtmlDecode(Regex.Replace(GetString(show, "summary"), "<[^>]*>", string.Empty));
            var poster = show.TryGetProperty("image", out var image) && image.ValueKind == JsonValueKind.Object
                ? GetString(image, "medium")
                : string.Empty;
            candidates.Add(new MuseProviderCandidate(value, title, GetString(show, "premiered"), summary, poster, "TVMaze", "Show"));
        }

        return candidates;
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }
}
