using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

public class WatchProviders
{
    [JsonPropertyName("provider_id")]
    public int ProviderId { get; set; }

    [JsonPropertyName("provider_name")]
    public string ProviderName { get; set; } = string.Empty;

    [JsonPropertyName("logo_path")]
    public string? LogoPath { get; set; }

    public string? Url { get; set; }
}
