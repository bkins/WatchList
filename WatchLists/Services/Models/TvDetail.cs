namespace WatchLists.Services.Models;

public class TvDetail
{
    public int         Id         { get; set; }
    public string      Name       { get; set; } = string.Empty;
    public string      Overview   { get; set; } = string.Empty;
    public string      PosterPath { get; set; } = string.Empty;
    public List<Genre> Genres     { get; set; } = new();
}
