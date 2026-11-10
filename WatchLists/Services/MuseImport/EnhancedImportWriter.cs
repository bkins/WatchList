using SQLite;
using WatchLists.MVVM.Models;
using WatchLists.Services.ReliableSync;

namespace WatchLists.Services.MuseImport;

public sealed class EnhancedImportWriter(SQLiteConnection connection)
{
    public EnhancedImportReceipt Apply(IReadOnlyList<EnhancedImportApproval> approvals, string backupDirectory)
    {
        if (approvals.Any(approval => approval.Row.Target is null)
            || approvals.GroupBy(approval => (approval.Row.Target!.Id, approval.Row.Entry.SourceKey)).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Each approved occurrence needs a distinct saved-item and source association.");
        if (approvals.Count == 0) return new EnhancedImportReceipt(0, 0, 0, null);

        Directory.CreateDirectory(backupDirectory);
        var backup = Path.Combine(backupDirectory, $"watchlist-before-enhanced-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db");
        connection.Backup(backup, "main");

        var added = 0;
        var enriched = 0;
        connection.RunInTransaction(() =>
        {
            connection.Execute("UPDATE WatchItems SET Id = Id WHERE 0");
            var rows = RecommendationStore.ReadExisting(connection);
            var review = new EnhancedImportReview(connection.Table<WatchItem>().ToList(), rows);
            foreach (var approval in approvals)
                CheckReview(review, approval.Row);

            _ = new RecommendationStore(connection);
            var set = new RecommendationSet(connection);
            var now = DateTime.UtcNow;
            foreach (var approval in approvals)
            {
                var row = approval.Row;
                var original = RecommendationMetadata.Read(row.Existing?.OriginalMetadata);
                var conflicts = row.Conflicts().ToHashSet();
                var changed = false;
                foreach (var field in row.Entry.Metadata)
                {
                    if (original.GetValueOrDefault(field.Key) == field.Value
                        || conflicts.Contains(field.Key) && !approval.AcceptedConflicts.Contains(field.Key))
                        continue;
                    original[field.Key] = field.Value;
                    changed = true;
                }
                if (!changed || row.Existing?.IsRemoved == true) continue;

                var itemId = row.Target!.Id;
                var current = set.Read(itemId);
                set.PreservePrimary(current);
                if (row.Existing is not null)
                {
                    var recommendation = current.Single(candidate => candidate.Id == row.Existing.Id);
                    recommendation.OriginalMetadata = RecommendationMetadata.Write(original);
                    connection.Update(recommendation);
                    enriched++;
                }
                else
                {
                    connection.Insert(new MovieRecommendation
                                      {
                                          WatchItemId      = itemId
                                        , SourceKey        = row.Entry.SourceKey
                                        , SourceUrl        = row.Entry.SourceUrl
                                        , OriginalMetadata = RecommendationMetadata.Write(original)
                                        , IsPrimary        = RecommendationSet.Primary(current) is null ? true : null
                                        , FirstImportedUtc = now
                                        , LastSeenUtc      = now
                                      });
                    added++;
                }
            }
        });
        return new EnhancedImportReceipt(added, enriched, approvals.Count - added - enriched, backup);
    }

    private static void CheckReview(EnhancedImportReview review, EnhancedImportRow expected)
    {
        var current = review.Preview(expected.Entry, expected.Target!.Id);
        if (current.Target is null || current.Target.Title != expected.Target.Title
            || current.Target.MovieId != expected.Target.MovieId || current.Target.Type != expected.Target.Type
            || current.Target.ApiSource != expected.Target.ApiSource
            || Fingerprint(current.Existing) != Fingerprint(expected.Existing))
            throw new InvalidOperationException("A saved item or recommendation changed after review. Reopen the file to review the latest copy.");
    }

    private static string? Fingerprint(MovieRecommendation? row)
        => row is null ? null : SyncJson.Write(SyncLink.From(row));
}
