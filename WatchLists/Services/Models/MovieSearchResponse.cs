using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

public class MovieSearchResponse
{
    [JsonPropertyName("results")]
    public List<MovieSearchResult>? Results { get; set; }
}
