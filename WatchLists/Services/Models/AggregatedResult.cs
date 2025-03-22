namespace WatchLists.Services.Models;

public class AggregatedResult<T>
{
    public T?            Data        { get; set; }
    public Dictionary<string?, string> Diagnostics { get; set; } = new();
}
