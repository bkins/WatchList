using SQLite;

namespace WatchLists.Services.MuseImport;

public sealed class MuseImportCoordinator
{
    private readonly SQLiteConnection _connection;
    private readonly Func<bool> _isAutoSyncEnabled;

    public MuseImportCoordinator(SQLiteConnection connection, Func<bool> isAutoSyncEnabled)
    {
        _connection        = connection ?? throw new ArgumentNullException(nameof(connection));
        _isAutoSyncEnabled = isAutoSyncEnabled ?? throw new ArgumentNullException(nameof(isAutoSyncEnabled));
    }

    public MuseImportReceipt Apply(MuseImportBatch batch, string backupDirectory, DateTime observedUtc)
    {
        if (_isAutoSyncEnabled())
        {
            throw new InvalidOperationException("Turn off automatic sync on this device before importing. Older versions can overwrite recommendation sources.");
        }
        if (string.IsNullOrWhiteSpace(backupDirectory)) throw new ArgumentException("A backup directory is required.", nameof(backupDirectory));

        Directory.CreateDirectory(backupDirectory);
        var name = $"watchlist-before-muse-{observedUtc:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db";
        return new MuseImportWriter(_connection).Apply(batch, Path.Combine(backupDirectory, name), observedUtc);
    }
}
