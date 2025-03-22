using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

public class MovieSearchResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("poster_path")]
    public string PosterPath { get; set; }

    [JsonPropertyName("overview")]
    public string Overview { get; set; }
}
