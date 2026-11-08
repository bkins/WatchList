using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

public class MovieSearchResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("poster_path")]
    public string PosterPath { get; set; } = string.Empty;

    [JsonPropertyName("overview")]
    public string Overview { get; set; } = string.Empty;

    [JsonPropertyName("release_date")]
    public string ReleaseDate { get; set; } = string.Empty;

    [JsonIgnore]
    public string MediaType { get; set; } = string.Empty;

    [JsonIgnore]
    public string PosterUrl => PosterPath.HasNoValue()
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

    [JsonIgnore]
    public List<string> SourceApis { get; set; } = new();

    [JsonIgnore]
    public bool HasSourceApis => SourceApis != null && SourceApis.Count > 0;

    [JsonIgnore]
    public string SourceApisDisplay => HasSourceApis
        ? "Source API: " + string.Join(", ", SourceApis)
        : string.Empty;

    [JsonIgnore]
    public string PrimarySourceApi { get; set; } = string.Empty;

    [JsonIgnore]
    public string WebUrl { get; set; } = string.Empty;

    [JsonIgnore]
    public Dictionary<string, string> AggregatedData { get; set; } = new();
}
