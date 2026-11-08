namespace WatchLists.Services.Models;

public class Provider
{
    public int    ProviderId   { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string LogoPath     { get; set; } = string.Empty;
    public string? Url          { get; set; }
}
