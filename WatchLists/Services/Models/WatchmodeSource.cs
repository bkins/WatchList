using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

internal sealed class WatchmodeSource
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }
}
