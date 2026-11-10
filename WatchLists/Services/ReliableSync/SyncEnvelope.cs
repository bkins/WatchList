namespace WatchLists.Services.ReliableSync;

public sealed record SyncEnvelope(int SchemaVersion, string Revision, string Payload)
{
    private const int LegacyVersion = 3;
    public const int CurrentVersion = 4;

    public static SyncEnvelope Create(SyncSnapshot snapshot, int? previousVersion = null)
    {
        var version = previousVersion == CurrentVersion || snapshot.Links.Any(link => link.IsEnhanced)
            ? CurrentVersion
            : LegacyVersion;
        return new SyncEnvelope(version, Guid.NewGuid().ToString("N"), SyncJson.Write(snapshot.Ordered()));
    }

    public SyncSnapshot Decode()
    {
        if (SchemaVersion is not (CurrentVersion or LegacyVersion) || !Guid.TryParseExact(Revision, "N", out _)
            || string.IsNullOrWhiteSpace(Payload))
            throw new InvalidDataException("Incompatible Firebase data. Pair upgraded devices with a fresh WatchList destination.");

        var snapshot = SyncJson.Read<SyncSnapshot>(Payload);
        SyncJson.Validate(snapshot);
        if (SchemaVersion == LegacyVersion && snapshot.Links.Any(link => link.IsEnhanced))
            throw new InvalidDataException("Enhanced recommendations require the newer sync format.");
        return snapshot;
    }
}
