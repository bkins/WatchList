namespace WatchLists.Services.Models;

public class TvDetail
{
    public int         Id         { get; set; }
    public string      Name       { get; set; }
    public string      Overview   { get; set; }
    public string      PosterPath { get; set; }
    public List<Genre> Genres     { get; set; } = new();
}
