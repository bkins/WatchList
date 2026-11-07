using WatchLists.MVVM.Models;

namespace WatchLists.Services.MuseImport;

public sealed class MuseImportReview
{
    private readonly IReadOnlyList<MuseRecommendation> _recommendations;
    private readonly Dictionary<int, MuseReviewDecision> _decisions = new();
    private readonly Dictionary<Guid, WatchItem> _newItems = new();

    public MuseImportReview(IReadOnlyList<MuseRecommendation> recommendations)
    {
        _recommendations = recommendations ?? throw new ArgumentNullException(nameof(recommendations));
    }

    public int ApprovedCount => _decisions.Values.Count(IsApproved);

    public MuseReviewDecision GetDecision(int index)
    {
        ValidateIndex(index);
        return _decisions.TryGetValue(index, out var decision)
            ? decision
            : new MuseReviewDecision(index, MuseReviewDecisionStatus.Pending);
    }

    public MuseReviewSummary GetSummary()
    {
        var approved = _decisions.Values.Where(IsApproved).ToList();
        return new MuseReviewSummary(_recommendations.Count
                                    , approved.Count
                                    , _newItems.Count
                                    , approved.Count(decision => decision.Status == MuseReviewDecisionStatus.ApprovedExisting)
                                    , _decisions.Values.Count(decision => decision.Status == MuseReviewDecisionStatus.Skipped)
                                    , _recommendations.Count - _decisions.Count
                                    , approved.Sum(decision => _recommendations[decision.RowIndex].SourceUrls.Count));
    }

    public void ApproveExisting(int index, WatchItem item)
    {
        ValidateRow(index);
        ArgumentNullException.ThrowIfNull(item);
        if (item.Id == Guid.Empty || item.IsDeleted)
        {
            throw new InvalidOperationException("A deleted or unidentified item cannot be selected for import.");
        }

        _decisions[index] = CreateDecision(index, item, MuseReviewDecisionStatus.ApprovedExisting);
        RemoveUnusedNewItems();
    }

    public void ApproveNew(int index, WatchItem item)
    {
        ValidateRow(index);
        ArgumentNullException.ThrowIfNull(item);
        if (item.Id == Guid.Empty || item.IsDeleted || item.MovieId <= 0 ||
            string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.ApiSource) || string.IsNullOrWhiteSpace(item.Category))
        {
            throw new InvalidOperationException("A new item requires an explicitly selected provider identity and category.");
        }

        var sameIdentity = _newItems.Values.FirstOrDefault(existing => existing.MovieId == item.MovieId &&
                                            string.Equals(existing.ApiSource, item.ApiSource, StringComparison.OrdinalIgnoreCase));
        var approvedItem = sameIdentity ?? item;
        approvedItem.Category = item.Category;
        _newItems[approvedItem.Id] = approvedItem;
        foreach (var decision in _decisions.Values.Where(decision => decision.ItemId == approvedItem.Id).ToList())
        {
            _decisions[decision.RowIndex] = decision with { Category = approvedItem.Category };
        }
        _decisions[index] = CreateDecision(index, approvedItem, MuseReviewDecisionStatus.ApprovedNew);
        RemoveUnusedNewItems();
    }

    public void Skip(int index)
    {
        ValidateIndex(index);
        _decisions[index] = new MuseReviewDecision(index, MuseReviewDecisionStatus.Skipped);
        RemoveUnusedNewItems();
    }

    public void SkipPending()
    {
        for (var index = 0; index < _recommendations.Count; index++)
        {
            if (!_decisions.ContainsKey(index)) Skip(index);
        }
    }

    public void ClearApproval(int index)
    {
        ValidateIndex(index);
        _decisions.Remove(index);
        RemoveUnusedNewItems();
    }

    public MuseImportBatch CreateFinalBatch()
    {
        if (GetSummary().PendingRows > 0)
        {
            throw new InvalidOperationException("Review or explicitly skip the remaining recommendations before importing.");
        }
        return CreateBatch();
    }

    public MuseImportBatch CreateBatch()
    {
        var entries = new List<RecommendationImportEntry>();
        foreach (var decision in _decisions.Values.Where(IsApproved).OrderBy(decision => decision.RowIndex))
        {
            var row = _recommendations[decision.RowIndex];
            foreach (var source in row.SourceUrls)
            {
                entries.Add(new RecommendationImportEntry(decision.ItemId!.Value, source, row.Description));
            }
        }

        if (entries.Count == 0) throw new InvalidOperationException("No recommendations have been approved.");
        return new MuseImportBatch(_newItems.Values.ToList(), entries);
    }

    private static bool IsApproved(MuseReviewDecision decision)
    {
        return decision.Status is MuseReviewDecisionStatus.ApprovedNew or MuseReviewDecisionStatus.ApprovedExisting;
    }

    private static MuseReviewDecision CreateDecision(int index, WatchItem item, MuseReviewDecisionStatus status)
    {
        return new MuseReviewDecision(index, status, item.Id, item.Title, item.MovieId, item.ApiSource, item.Type, item.Category);
    }

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= _recommendations.Count) throw new ArgumentOutOfRangeException(nameof(index));
    }

    private void ValidateRow(int index)
    {
        ValidateIndex(index);
        if (_recommendations[index].SourceUrls.Count == 0)
        {
            throw new InvalidOperationException("A recommendation without a source URL needs correction before import.");
        }
    }

    private void RemoveUnusedNewItems()
    {
        var referenced = _decisions.Values.Where(IsApproved).Select(decision => decision.ItemId).ToHashSet();
        foreach (var itemId in _newItems.Keys.Where(itemId => !referenced.Contains(itemId)).ToList())
        {
            _newItems.Remove(itemId);
        }
    }
}
