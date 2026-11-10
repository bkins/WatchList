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

    public static List<MovieRecommendation> ReadExisting(SQLiteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var exists = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?", "MovieRecommendation");
        return exists == 0 ? [] : connection.Table<MovieRecommendation>().ToList();
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

    public int MergeFromSync(IEnumerable<MovieRecommendation> incoming
                           , IReadOnlyDictionary<Guid, Guid> itemMapping)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(itemMapping);

        var batch = incoming.ToList();
        var insertedCount = 0;
        _connection.RunInTransaction(() =>
        {
            foreach (var recommendation in batch)
            {
                if (!itemMapping.TryGetValue(recommendation.WatchItemId, out var localId) || localId == Guid.Empty ||
                    _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM WatchItems WHERE Id = ?", localId) != 1)
                {
                    throw new InvalidOperationException("Recommendation sync has no uniquely identified local item.");
                }

                var descriptions = JsonSerializer.Deserialize<List<string>>(recommendation.DescriptionsJson);
                if (descriptions == null || descriptions.Count == 0 || descriptions.Any(string.IsNullOrWhiteSpace))
                {
                    throw new InvalidOperationException("Recommendation sync has invalid descriptions.");
                }

                var firstSeen = ToUtc(recommendation.FirstImportedUtc);
                var lastSeen  = ToUtc(recommendation.LastSeenUtc);
                if (firstSeen == default || lastSeen < firstSeen)
                {
                    throw new InvalidOperationException("Recommendation sync has invalid timestamps.");
                }

                foreach (var description in descriptions)
                {
                    if (AddOrUpdate(localId, recommendation.SourceUrl, description, lastSeen)) insertedCount++;
                }

                var sourceKey = RecommendationSourceKey.Create(recommendation.SourceUrl);
                var merged = _connection.Table<MovieRecommendation>()
                                        .First(item => item.WatchItemId == localId && item.SourceKey == sourceKey);
                if (firstSeen < ToUtc(merged.FirstImportedUtc))
                {
                    merged.FirstImportedUtc = firstSeen;
                    _connection.Update(merged);
                }
            }
        });
        return insertedCount;
    }

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private bool AddOrUpdate(Guid watchItemId, string sourceUrl, string description, DateTime observedUtc)
    {
        if (watchItemId == Guid.Empty) throw new ArgumentException("A watch item is required.", nameof(watchItemId));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("A description is required.", nameof(description));
        if (observedUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Observation time must be UTC.", nameof(observedUtc));

        var sourceKey = RecommendationSourceKey.Create(sourceUrl);
        var existing  = _connection.Table<MovieRecommendation>()
                                   .FirstOrDefault(item => item.WatchItemId == watchItemId && item.SourceKey == sourceKey);
        if (existing?.IsRemoved == true) return false;
        if (existing == null)
        {
            var set = new RecommendationSet(_connection);
            var rows = set.Read(watchItemId);
            if (RecommendationSet.Primary(rows) is { } primary && observedUtc <= primary.FirstImportedUtc)
                set.PreservePrimary(rows);
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
