using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

public class UtellySearchResult
{
    [JsonPropertyName("name")]
    public string Title { get; set; }

    [JsonPropertyName("locations")]
    public List<UtellyProvider>? Providers { get; set; }
}
