namespace WatchLists.Services.Models;

public class WatchProviderInfo
{
    public List<Provider> Flatrate { get; set; } = new();
    public List<Provider> Rent     { get; set; } = new();
    public List<Provider> Buy      { get; set; } = new();
}
