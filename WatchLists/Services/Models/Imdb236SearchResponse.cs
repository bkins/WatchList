using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

internal sealed class Imdb236SearchResponse
{
    [JsonPropertyName("results")]
    public List<Imdb236SearchResult>? Results { get; set; }
}
