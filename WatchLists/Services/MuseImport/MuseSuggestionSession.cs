namespace WatchLists.Services.MuseImport;

public sealed class MuseSuggestionSession
{
    private readonly object _sync = new();
    private readonly IReadOnlyList<MuseRecommendation> _rows;
    private readonly Dictionary<MuseRecommendation, MuseSuggestionResult> _completed = new();
    private bool _running;

    public MuseImportReview Review { get; private set; }

    public MuseSuggestionSession(IReadOnlyList<MuseRecommendation> rows)
    {
        _rows = rows ?? throw new ArgumentNullException(nameof(rows));
        Review = new MuseImportReview(rows);
    }

    public IReadOnlyDictionary<MuseRecommendation, MuseSuggestionResult> Completed
    {
        get
        {
            lock (_sync) return new Dictionary<MuseRecommendation, MuseSuggestionResult>(_completed);
        }
    }

    public void ResetApprovals()
    {
        Review = new MuseImportReview(_rows);
    }

    public async Task SearchPendingAsync(Func<string, CancellationToken, Task<IReadOnlyList<MuseProviderCandidate>>> search
                                       , Action<MuseSuggestionResult> onCompleted
                                       , CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(onCompleted);
        List<MuseRecommendation> pending;
        lock (_sync)
        {
            if (_running) throw new InvalidOperationException("A search is already running.");
            _running = true;
            pending = _rows.Where(row => !_completed.ContainsKey(row) || _completed[row].Error != null).ToList();
        }

        try
        {
            await new MuseSuggestionPlanner().SuggestAsync(pending, search, cancellationToken, result =>
            {
                MuseSuggestionResult combined;
                lock (_sync)
                {
                    if (_completed.TryGetValue(result.Recommendation, out var previous))
                    {
                        var candidates = result.Candidates.Concat(previous.Candidates)
                                               .GroupBy(candidate => (candidate.Provider.ToUpperInvariant(), candidate.MediaType.ToUpperInvariant(), candidate.Id))
                                               .Select(group => group.First())
                                               .ToList();
                        combined = new MuseSuggestionResult(result.Recommendation, candidates, result.Error);
                    }
                    else combined = result;
                    _completed[result.Recommendation] = combined;
                }
                onCompleted(combined);
            });
        }
        finally
        {
            lock (_sync) _running = false;
        }
    }
}
