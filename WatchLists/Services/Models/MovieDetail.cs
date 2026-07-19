using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

public class MovieDetail
{
    [JsonPropertyName("id")]
    public int         Id          { get; set; }

    [JsonPropertyName("title")]
    public string      Title       { get; set; }

    [JsonPropertyName("overview")]
    public string      Overview    { get; set; }

    [JsonPropertyName("poster_path")]
    public string?     PosterPath  { get; set; }

    [JsonPropertyName("genres")]
    public List<Genre> Genres      { get; set; } = new();

    [JsonPropertyName("release_date")]
    public string?     ReleaseDate { get; set; }
}
