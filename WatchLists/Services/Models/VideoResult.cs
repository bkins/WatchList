namespace WatchLists.Services.Models;

public class VideoResult
{
    public int         Id      { get; set; }
    public List<Video> Results { get; set; } = new();
}
