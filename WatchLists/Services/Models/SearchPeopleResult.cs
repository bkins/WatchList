namespace WatchLists.Services.Models;

public class SearchPeopleResult
{
    public int          Page         { get; set; }
    public List<Person> Results      { get; set; } = new();
    public int          TotalResults { get; set; }
    public int          TotalPages   { get; set; }
}
