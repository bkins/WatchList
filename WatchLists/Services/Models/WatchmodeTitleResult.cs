using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

internal sealed class WatchmodeTitleResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("year")]
    public int? Year { get; set; }
}
