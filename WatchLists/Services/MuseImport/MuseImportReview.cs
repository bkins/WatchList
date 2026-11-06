using WatchLists.MVVM.Models;

namespace WatchLists.Services.MuseImport;

public sealed class MuseImportReview
{
    private readonly IReadOnlyList<MuseRecommendation> _recommendations;
    private readonly Dictionary<int, Guid> _approvals = new();
    private readonly Dictionary<Guid, WatchItem> _newItems = new();

    public MuseImportReview(IReadOnlyList<MuseRecommendation> recommendations)
    {
        _recommendations = recommendations ?? throw new ArgumentNullException(nameof(recommendations));
    }

    public int ApprovedCount => _approvals.Count;

    public void ApproveExisting(int index, WatchItem item)
    {
        ValidateRow(index);
        ArgumentNullException.ThrowIfNull(item);
        if (item.Id == Guid.Empty || item.IsDeleted)
        {
            throw new InvalidOperationException("A deleted or unidentified item cannot be selected for import.");
        }

        _approvals[index] = item.Id;
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
        _newItems[approvedItem.Id] = approvedItem;
        _approvals[index] = approvedItem.Id;
        RemoveUnusedNewItems();
    }

    public void ClearApproval(int index)
    {
        ValidateRow(index);
        _approvals.Remove(index);
        RemoveUnusedNewItems();
    }

    public MuseImportBatch CreateBatch()
    {
        var entries = new List<RecommendationImportEntry>();
        foreach (var approval in _approvals.OrderBy(approval => approval.Key))
        {
            var row = _recommendations[approval.Key];
            foreach (var source in row.SourceUrls)
            {
                entries.Add(new RecommendationImportEntry(approval.Value, source, row.Description));
            }
        }

        if (entries.Count == 0) throw new InvalidOperationException("No recommendations have been approved.");
        return new MuseImportBatch(_newItems.Values.ToList(), entries);
    }

    private void ValidateRow(int index)
    {
        if (index < 0 || index >= _recommendations.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (_recommendations[index].SourceUrls.Count == 0)
        {
            throw new InvalidOperationException("A recommendation without a source URL needs correction before import.");
        }
    }

    private void RemoveUnusedNewItems()
    {
        foreach (var itemId in _newItems.Keys.Where(itemId => !_approvals.ContainsValue(itemId)).ToList())
        {
            _newItems.Remove(itemId);
        }
    }
}
