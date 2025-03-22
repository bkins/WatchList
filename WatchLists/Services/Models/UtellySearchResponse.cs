using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

public class UtellySearchResponse
{
    [JsonPropertyName("results")]
    public List<UtellySearchResult>? Results { get; set; }
}
