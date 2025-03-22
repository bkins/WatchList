namespace WatchLists.Services.Models;

public class MovieDetail
{
    public int         Id          { get; set; }
    public string      Title       { get; set; }
    public string      Overview    { get; set; }
    public string      PosterPath  { get; set; }
    public List<Genre> Genres      { get; set; } = new();
    public string      ReleaseDate { get; set; }
}
