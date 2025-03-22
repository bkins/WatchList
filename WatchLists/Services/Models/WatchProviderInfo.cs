namespace WatchLists.Services.Models;

public class WatchProviderInfo
{
    public List<Provider> Flatrate { get; set; }
    public List<Provider> Rent     { get; set; }
    public List<Provider> Buy      { get; set; }
}
