namespace WatchLists.Services.Models;

public class TvShow
{
    public int    Id         { get; set; }
    public string Name       { get; set; } = string.Empty;
    public string Overview   { get; set; } = string.Empty;
    public string PosterPath { get; set; } = string.Empty;
}
