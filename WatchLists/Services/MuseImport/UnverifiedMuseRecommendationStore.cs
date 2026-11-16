using SQLite;

namespace WatchLists.Services.MuseImport;

public sealed class UnverifiedMuseRecommendationStore
{
    private readonly SQLiteConnection _connection;

    public UnverifiedMuseRecommendationStore(SQLiteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connection.CreateTable<UnverifiedMuseRecommendation>();
    }

    public IReadOnlyList<UnverifiedMuseRecommendation> GetAll()
    {
        return _connection.Table<UnverifiedMuseRecommendation>()
                          .OrderBy(item => item.Title)
                          .ThenBy(item => item.ReleaseYear)
                          .ToList();
    }

    public static List<UnverifiedMuseRecommendation> ReadExisting(SQLiteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var exists = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?", "UnverifiedMuseRecommendation");
        return exists == 0 ? [] : connection.Table<UnverifiedMuseRecommendation>().ToList();
    }

    public int AddOrUpdate(IEnumerable<MuseRecommendation> recommendations, DateTime observedUtc)
    {
        ArgumentNullException.ThrowIfNull(recommendations);
        if (observedUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Observation time must be UTC.", nameof(observedUtc));

        var additions = recommendations.ToList();
        if (additions.Any(item => string.IsNullOrWhiteSpace(item.Title) || item.SourceUrls.Any(string.IsNullOrWhiteSpace)))
            throw new ArgumentException("Every unverified entry needs a title and valid source URLs.", nameof(recommendations));

        var changed = 0;
        _connection.RunInTransaction(() =>
        {
            foreach (var recommendation in additions)
            {
                IReadOnlyList<string> sourceUrls = recommendation.SourceUrls.Count == 0 ? new[] { string.Empty } : recommendation.SourceUrls;
                foreach (var sourceUrl in sourceUrls)
                {
                    var titleKey = $"{recommendation.Title.Trim().ToUpperInvariant()}:{recommendation.ReleaseYear?.ToString() ?? "unknown"}";
                    var sourceKey = sourceUrl.Length == 0
                        ? $"title:{titleKey}"
                        : $"{RecommendationSourceKey.Create(sourceUrl)}|{titleKey}";
                    var existing = _connection.Table<UnverifiedMuseRecommendation>()
                                              .FirstOrDefault(item => item.SourceKey == sourceKey);
                    if (existing == null)
                    {
                        _connection.Insert(new UnverifiedMuseRecommendation
                                           {
                                               SourceKey        = sourceKey
                                             , SourceUrl        = sourceUrl
                                             , Title            = recommendation.Title
                                             , ReleaseYear      = recommendation.ReleaseYear
                                             , Description      = recommendation.Description
                                             , Section          = recommendation.Section
                                             , SourceLineNumber = recommendation.LineNumber
                                             , FirstImportedUtc = observedUtc
                                             , LastSeenUtc      = observedUtc
                                           });
                        changed++;
                        continue;
                    }

                    if (existing.Title != recommendation.Title || existing.ReleaseYear != recommendation.ReleaseYear ||
                        existing.Description != recommendation.Description || existing.Section != recommendation.Section)
                        changed++;
                    existing.Title            = recommendation.Title;
                    existing.SourceUrl         = sourceUrl;
                    existing.ReleaseYear      = recommendation.ReleaseYear;
                    existing.Description      = recommendation.Description;
                    existing.Section          = recommendation.Section;
                    existing.SourceLineNumber = recommendation.LineNumber;
                    existing.LastSeenUtc      = observedUtc;
                    _connection.Update(existing);
                }
            }
        });
        return changed;
    }

    public bool Remove(Guid id)
    {
        return _connection.Delete<UnverifiedMuseRecommendation>(id) > 0;
    }
}
