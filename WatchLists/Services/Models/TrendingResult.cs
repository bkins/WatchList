namespace WatchLists.Services.Models;

public class TrendingResult
{
    public int         Page         { get; set; }
    public List<Movie> Results      { get; set; } = new();
    public int         TotalResults { get; set; }
    public int         TotalPages   { get; set; }
}
