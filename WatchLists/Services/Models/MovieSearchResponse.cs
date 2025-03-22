using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

public class MovieSearchResponse
{
    [JsonPropertyName("results")]
    public Dictionary<string, MovieSearchResult>? Results { get; set; }
}
