namespace WatchLists.Services.Models;

public class Video
{
    public string Id         { get; set; } = string.Empty;
    public string Iso_639_1  { get; set; } = string.Empty;
    public string Iso_3166_1 { get; set; } = string.Empty;
    public string Key        { get; set; } = string.Empty; // e.g. YouTube key
    public string Name       { get; set; } = string.Empty;
    public string Site       { get; set; } = string.Empty; // e.g., YouTube.com
    public int    Size       { get; set; } // Video quality (e.g., 1080)
    public string Type       { get; set; } = string.Empty; // e.g., Trailer, Teaser
}
