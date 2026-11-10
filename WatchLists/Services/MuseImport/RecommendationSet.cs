using SQLite;

namespace WatchLists.Services.MuseImport;

public sealed class RecommendationSet(SQLiteConnection connection)
{
    public static MovieRecommendation? Primary(IEnumerable<MovieRecommendation> recommendations)
    {
        return recommendations.Where(recommendation => recommendation.IsRemoved != true)
                              .OrderByDescending(recommendation => recommendation.IsPrimary == true)
                              .ThenBy(recommendation => recommendation.FirstImportedUtc)
                              .ThenBy(recommendation => recommendation.Id)
                              .FirstOrDefault();
    }

    public MovieRecommendation[] Read(Guid itemId)
    {
        return connection.Table<MovieRecommendation>()
                         .Where(recommendation => recommendation.WatchItemId == itemId)
                         .ToArray();
    }

    public void CheckActiveItem(Guid itemId)
    {
        if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM WatchItems WHERE Id = ? AND COALESCE(IsDeleted, 0) = 0", itemId) != 1)
            throw new InvalidOperationException("The selected watch item is missing or deleted.");
    }

    public void Save(Guid                                      itemId
                   , Guid?                                     recommendationId
                   , IReadOnlyDictionary<RecommendationField, string> values
                   , string?                                   expectedEdits
                   , string?                                   expectedOriginal
                   , string?                                   expectedDescriptions = null)
    {
        InTransaction(itemId, rows =>
        {
            if (recommendationId is null)
            {
                var id = Guid.NewGuid();
                var now = DateTime.UtcNow;
                connection.Insert(new MovieRecommendation
                                  {
                                      Id               = id
                                    , WatchItemId      = itemId
                                    , SourceKey        = $"manual:{id:N}"
                                    , EditedMetadata   = RecommendationMetadata.Write(values)
                                    , IsPrimary        = Primary(rows) is null ? true : null
                                    , FirstImportedUtc = now
                                    , LastSeenUtc      = now
                                  });
                return;
            }

            var recommendation = Active(rows, recommendationId.Value);
            if (recommendation.EditedMetadata != expectedEdits || recommendation.OriginalMetadata != expectedOriginal
                || expectedDescriptions is not null && recommendation.DescriptionsJson != expectedDescriptions)
                throw new InvalidOperationException("The recommendation changed. Reopen it to edit the latest copy.");
            recommendation.EditedMetadata = RecommendationMetadata.Write(values);
            connection.Update(recommendation);
        });
    }

    public void SelectPrimary(Guid itemId, Guid recommendationId)
    {
        InTransaction(itemId, rows => SetPrimary(rows, Active(rows, recommendationId)));
    }

    public void Remove(Guid itemId, Guid recommendationId, Guid? replacementId)
    {
        InTransaction(itemId, rows =>
        {
            var recommendation = Active(rows, recommendationId);
            if (Primary(rows)?.Id == recommendationId && rows.Any(row => row.Id != recommendationId && row.IsRemoved != true))
            {
                if (replacementId is null || replacementId == recommendationId)
                    throw new InvalidOperationException("Choose a replacement primary before deleting this recommendation.");
                SetPrimary(rows, Active(rows, replacementId.Value));
            }
            recommendation.IsRemoved = true;
            recommendation.IsPrimary = null;
            connection.Update(recommendation);
        });
    }

    public void PreservePrimary(MovieRecommendation[] rows)
    {
        var primary = Primary(rows);
        if (primary is not null && primary.IsPrimary != true)
            SetPrimary(rows, primary);
    }

    private void SetPrimary(MovieRecommendation[] rows, MovieRecommendation primary)
    {
        foreach (var row in rows)
        {
            bool? selected = row.Id == primary.Id ? true : null;
            if (row.IsPrimary == selected) continue;
            row.IsPrimary = selected;
            connection.Update(row);
        }
    }

    private void InTransaction(Guid itemId, Action<MovieRecommendation[]> work)
    {
        connection.RunInTransaction(() =>
        {
            // Take the writer reservation before reading the current recommendation set.
            connection.Execute("UPDATE WatchItems SET Id = Id WHERE Id = ?", itemId);
            CheckActiveItem(itemId);
            var rows = Read(itemId);
            PreservePrimary(rows);
            work(rows);
        });
    }

    private static MovieRecommendation Active(MovieRecommendation[] rows, Guid id)
    {
        return rows.FirstOrDefault(row => row.Id == id && row.IsRemoved != true)
            ?? throw new InvalidOperationException("The recommendation is no longer available.");
    }
}
