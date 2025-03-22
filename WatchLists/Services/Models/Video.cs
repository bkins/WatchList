namespace WatchLists.Services.Models;

public class Video
{
    public string Id         { get; set; }
    public string Iso_639_1  { get; set; }
    public string Iso_3166_1 { get; set; }
    public string Key        { get; set; } // e.g. YouTube key
    public string Name       { get; set; }
    public string Site       { get; set; } // e.g., YouTube.com
    public int    Size       { get; set; } // Video quality (e.g., 1080)
    public string Type       { get; set; } // e.g., Trailer, Teaser
}
