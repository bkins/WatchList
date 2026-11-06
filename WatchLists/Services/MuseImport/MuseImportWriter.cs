using SQLite;
using WatchLists.MVVM.Models;

namespace WatchLists.Services.MuseImport;

public sealed class MuseImportWriter
{
    private readonly SQLiteConnection _connection;

    public MuseImportWriter(SQLiteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public MuseImportReceipt Apply(MuseImportBatch batch, string backupPath, DateTime observedUtc)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (string.IsNullOrWhiteSpace(backupPath)) throw new ArgumentException("A backup path is required.", nameof(backupPath));
        if (File.Exists(backupPath)) throw new IOException("The backup path already exists.");
        if (observedUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Observation time must be UTC.", nameof(observedUtc));
        if (batch.NewItems == null || batch.Recommendations == null || batch.Recommendations.Count == 0)
        {
            throw new ArgumentException("An approved import must contain recommendation sources.", nameof(batch));
        }
        if (batch.NewItems.Any(item => item == null || !batch.Recommendations.Any(entry => entry.WatchItemId == item.Id)))
        {
            throw new ArgumentException("Every new item must have an approved recommendation source.", nameof(batch));
        }

        _connection.Backup(backupPath, "main");

        var recommendationCount = 0;
        _connection.RunInTransaction(() =>
        {
            var store = new RecommendationStore(_connection);
            foreach (var item in batch.NewItems)
            {
                ValidateNewItem(item);
                _connection.Insert(item);
            }

            recommendationCount = store.Apply(batch.Recommendations, observedUtc);
        });

        return new MuseImportReceipt(batch.NewItems.Count, recommendationCount, backupPath);
    }

    private void ValidateNewItem(WatchItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Title) || item.IsDeleted ||
            item.MovieId <= 0 || string.IsNullOrWhiteSpace(item.ApiSource) || string.IsNullOrWhiteSpace(item.Category))
        {
            throw new InvalidOperationException("A new item needs an active identity, category and confirmed provider ID.");
        }

        var matches = _connection.Table<WatchItem>()
                                 .Where(existing => existing.MovieId == item.MovieId)
                                 .ToList();
        if (matches.Any(existing => string.IsNullOrWhiteSpace(existing.ApiSource) ||
                                    string.Equals(existing.ApiSource, item.ApiSource, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("The confirmed provider identity already exists, including deleted items.");
        }
    }
}
