namespace WatchLists.Services.ReliableSync;

public sealed record SyncEnvelope(int SchemaVersion, string Revision, string Payload)
{
    public const int CurrentVersion = 3;

    public static SyncEnvelope Create(SyncSnapshot snapshot)
        => new(CurrentVersion, Guid.NewGuid().ToString("N"), SyncJson.Write(snapshot.Ordered()));

    public SyncSnapshot Decode()
    {
        if (SchemaVersion != CurrentVersion || !Guid.TryParseExact(Revision, "N", out _)
            || string.IsNullOrWhiteSpace(Payload))
            throw new InvalidDataException("Incompatible Firebase data. Pair upgraded devices with a fresh WatchList destination.");

        var snapshot = SyncJson.Read<SyncSnapshot>(Payload);
        SyncJson.Validate(snapshot);
        return snapshot;
    }
}
