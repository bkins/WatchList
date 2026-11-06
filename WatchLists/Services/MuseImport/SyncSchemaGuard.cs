namespace WatchLists.Services.MuseImport;

public static class SyncSchemaGuard
{
    public const int CurrentVersion = 2;

    public static void Validate(int? version)
    {
        if (version != CurrentVersion)
        {
            throw new InvalidDataException("Sync payload is from an incompatible app version. Upgrade all devices and use a compatible sync destination.");
        }
    }
}
