namespace WatchLists.Services.MuseImport;

public sealed record MuseProviderCandidate(int Id
                                         , string Title
                                         , string Date
                                         , string Overview
                                         , string PosterUrl
                                         , string Provider
                                         , string MediaType)
{
    public string Display => $"{Title} ({Date}) · {Provider} {MediaType} {Id}";
}
