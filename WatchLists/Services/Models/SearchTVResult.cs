namespace WatchLists.Services.Models;

public class SearchTVResult
{
    public int          Page         { get; set; }
    public List<TvShow> Results      { get; set; } = new();
    public int          TotalResults { get; set; }
    public int          TotalPages   { get; set; }
}
