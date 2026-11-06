namespace WatchLists.Services.MuseImport;

public sealed class PartialProviderSearchException : Exception
{
    public IReadOnlyList<MuseProviderCandidate> Candidates { get; }

    public PartialProviderSearchException(string message, IReadOnlyList<MuseProviderCandidate> candidates) : base(message)
    {
        Candidates = candidates;
    }
}
