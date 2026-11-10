using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;

namespace WatchLists.Services;

public partial class WatchListService
{
    public MovieRecommendation[] GetRecommendations(Guid itemId, long generation)
    {
        lock (_databaseLock)
        {
            CheckRecommendationGeneration(generation);
            if (Database.Find<WatchItem>(itemId) is not { IsDeleted: false }) return [];
            return RecommendationStore.ReadExisting(Database)
                                      .Where(row => row.WatchItemId == itemId && row.IsRemoved != true)
                                      .ToArray();
        }
    }

    public ILookup<Guid, string> GetRecommendationSearchIndex()
    {
        lock (_databaseLock)
        {
            return RecommendationStore.ReadExisting(Database)
                                      .SelectMany(row => RecommendationMetadata.SearchValues(row)
                                          .Select(value => (row.WatchItemId, Value: value)))
                                      .ToLookup(entry => entry.WatchItemId, entry => entry.Value);
        }
    }

    public void SaveRecommendation(Guid itemId, long generation, MovieRecommendation? original
                                 , IReadOnlyDictionary<RecommendationField, string> values)
    {
        UpdateRecommendations(generation, set => set.Save(itemId
                                                         , original?.Id
                                                         , values
                                                         , original?.EditedMetadata
                                                         , original?.OriginalMetadata
                                                         , original?.DescriptionsJson));
    }

    public void SetPrimaryRecommendation(Guid itemId, long generation, Guid recommendationId)
    {
        UpdateRecommendations(generation, set => set.SelectPrimary(itemId, recommendationId));
    }

    public void RemoveRecommendation(Guid itemId, long generation, Guid recommendationId, Guid? replacementId)
    {
        UpdateRecommendations(generation, set => set.Remove(itemId, recommendationId, replacementId));
    }

    public (EnhancedImportReview Review, long Generation) CreateEnhancedImportReview()
    {
        lock (_databaseLock)
        {
            var review = new EnhancedImportReview(Database.Table<WatchItem>().ToList(), RecommendationStore.ReadExisting(Database));
            return (review, DatabaseGeneration);
        }
    }

    public Task<EnhancedImportReceipt> ApplyEnhancedImportAsync(IReadOnlyList<EnhancedImportApproval> approvals, long generation)
    {
        return SyncAndImportGate.RunAsync(() => Task.Run(() =>
        {
            EnhancedImportReceipt receipt;
            lock (_databaseLock)
            {
                CheckRecommendationGeneration(generation);
                if (_settingsService.GetAutoSyncEnabled())
                    throw new InvalidOperationException("Turn off automatic sync before importing recommendations.");
                var folder = Path.Combine(Path.GetDirectoryName(_databasePath)!, "ImportBackups");
                receipt = new EnhancedImportWriter(Database).Apply(approvals, folder);
            }
            if (receipt.Added + receipt.Enriched > 0) NotifyAfterDatabaseReplacement();
            return receipt;
        }));
    }

    private void UpdateRecommendations(long generation, Action<RecommendationSet> update)
    {
        lock (_databaseLock)
        {
            CheckRecommendationGeneration(generation);
            _ = new RecommendationStore(Database);
            update(new RecommendationSet(Database));
        }
        NotifyAfterDatabaseReplacement();
    }

    private void CheckRecommendationGeneration(long generation)
    {
        if (!IsCurrentGeneration(generation))
            throw new InvalidOperationException("The WatchList was restored. Reopen this screen before working with recommendations.");
    }
}
