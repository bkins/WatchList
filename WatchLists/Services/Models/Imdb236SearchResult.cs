using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

internal sealed class Imdb236SearchResult
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("primaryTitle")]
    public string? PrimaryTitle { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("primaryImage")]
    public string? PrimaryImage { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }
}
