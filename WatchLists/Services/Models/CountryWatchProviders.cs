namespace WatchLists.Services.Models;

public class CountryWatchProviders
{
    public List<WatchProviders>? Flatrate { get; set; }
    public List<WatchProviders>? Rent     { get; set; }
    public List<WatchProviders>? Buy      { get; set; }
}
