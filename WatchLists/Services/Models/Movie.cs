namespace WatchLists.Services.Models;

public class Movie
{
    public int    Id         { get; set; }
    public string Title      { get; set; }
    public string       Overview           { get; set; }
    public string       PosterPath         { get; set; }
    public List<string> StreamingProviders { get; set; } = new();
    public string       PrimarySourceApi   { get; set; } = string.Empty;
    public string       WebUrl             { get; set; } = string.Empty;
    public string       MediaType          { get; set; } = string.Empty;
}
