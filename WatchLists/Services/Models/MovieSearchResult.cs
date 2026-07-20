using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

public class MovieSearchResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("poster_path")]
    public string PosterPath { get; set; }

    [JsonPropertyName("overview")]
    public string Overview { get; set; }

    [JsonIgnore]
    public string PosterUrl => string.IsNullOrWhiteSpace(PosterPath)
        ? string.Empty
        : PosterPath.StartsWith("http") ? PosterPath : $"https://image.tmdb.org/t/p/w500{PosterPath}";

    [JsonIgnore]
    public List<string> StreamingProviders { get; set; } = new();

    [JsonIgnore]
    public bool HasStreamingProviders => StreamingProviders != null && StreamingProviders.Count > 0;

    [JsonIgnore]
    public string StreamingProvidersDisplay => StreamingProviders != null && StreamingProviders.Count > 0
        ? "Available on: " + string.Join(", ", StreamingProviders)
        : string.Empty;
}
