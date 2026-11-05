using System.Text.Json;
using SQLite;

namespace WatchLists.Services.MuseImport;

public sealed class RecommendationStore
{
    private readonly SQLiteConnection _connection;

    public RecommendationStore(SQLiteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connection.CreateTable<MovieRecommendation>();
    }

    public IReadOnlyList<MovieRecommendation> GetAll()
    {
        return _connection.Table<MovieRecommendation>().ToList();
    }

    public int Apply(IEnumerable<RecommendationImportEntry> entries, DateTime observedUtc)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (observedUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Observation time must be UTC.", nameof(observedUtc));

        var batch = entries.ToList();
        var insertedCount = 0;
        _connection.RunInTransaction(() =>
        {
            foreach (var entry in batch)
            {
                var activeCount = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM WatchItems WHERE Id = ? AND COALESCE(IsDeleted, 0) = 0", entry.WatchItemId);
                if (activeCount != 1) throw new InvalidOperationException("The selected watch item is missing or deleted.");
                if (AddOrUpdate(entry.WatchItemId, entry.SourceUrl, entry.Description, observedUtc)) insertedCount++;
            }
        });
        return insertedCount;
    }

    private bool AddOrUpdate(Guid watchItemId, string sourceUrl, string description, DateTime observedUtc)
    {
        if (watchItemId == Guid.Empty) throw new ArgumentException("A watch item is required.", nameof(watchItemId));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("A description is required.", nameof(description));
        if (observedUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Observation time must be UTC.", nameof(observedUtc));

        var sourceKey = RecommendationSourceKey.Create(sourceUrl);
        var existing  = _connection.Table<MovieRecommendation>()
                                   .FirstOrDefault(item => item.WatchItemId == watchItemId && item.SourceKey == sourceKey);
        if (existing == null)
        {
            var recommendation = new MovieRecommendation
                                 {
                                     WatchItemId       = watchItemId
                                   , SourceKey         = sourceKey
                                   , SourceUrl         = sourceUrl
                                   , DescriptionsJson  = JsonSerializer.Serialize(new[] { description.Trim() })
                                   , FirstImportedUtc  = observedUtc
                                   , LastSeenUtc       = observedUtc
                                 };
            _connection.Insert(recommendation);
            return true;
        }

        var descriptions = JsonSerializer.Deserialize<List<string>>(existing.DescriptionsJson)
                           ?? throw new InvalidOperationException("Recommendation descriptions are invalid.");
        if (!descriptions.Contains(description.Trim(), StringComparer.Ordinal))
        {
            descriptions.Add(description.Trim());
            existing.DescriptionsJson = JsonSerializer.Serialize(descriptions);
        }
        if (observedUtc > existing.LastSeenUtc) existing.LastSeenUtc = observedUtc;
        _connection.Update(existing);
        return false;
    }
}
