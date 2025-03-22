using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

public class UtellyProvider
{
    [JsonPropertyName("display_name")]
    public string ProviderName { get; set; }

    [JsonPropertyName("url")]
    public string ProviderUrl { get; set; }
}
