using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using WatchLists.Services.MuseImport;

namespace WatchLists.MVVM.ViewModels;

public partial class MuseImportPreviewViewModel
{
    private readonly Dictionary<int, MuseReviewSelection> _rowSelections = new();
    private readonly Dictionary<int, IReadOnlyList<MuseProviderCandidate>> _correctedResults = new();
    private int _wizardStep = 1;
    private bool _restoringSelection;
    private string _importBackupPath = string.Empty;
    public string ImportBackupPath => _importBackupPath;
    private string _fileName = string.Empty;
    private string _fileInformation = string.Empty;
    private string _searchQuery = string.Empty;

    public ObservableCollection<MuseReviewRow> ReviewRows { get; } = new();
    public int WizardStep => _wizardStep;
    public bool IsFileStep => _wizardStep == 1;
    public bool IsReviewStep => _wizardStep == 2;
    public bool IsSummaryStep => _wizardStep == 3;
    public bool IsResultStep => _wizardStep == 4;
    public bool HasReview => _review != null;
    public bool HasFile => _review != null && Recommendations.Count > 0;
    public bool CanChangeReview => !IsBusy;
    public string StepHeading
    {
        get
        {
            var title = _wizardStep switch
            {
                1 => "Select file"
              , 2 => "Review recommendations"
              , 3 => "Review summary"
              , _ => "Import results"
            };
            return $"{_wizardStep} of 4 - {title}";
        }
    }
    public string FileName => _fileName;
    public string FileInformation => _fileInformation;
    public string RecommendationPosition => $"Recommendation {CurrentIndex + 1} of {Recommendations.Count}";
    public string RecommendationTitle
    {
        get
        {
            if (SelectedRecommendation == null) return string.Empty;
            var year = SelectedRecommendation.ReleaseYear;
            return SelectedRecommendation.Title + (year.HasValue ? $" ({year})" : string.Empty);
        }
    }
    public string RecommendationDescription => SelectedRecommendation?.Description ?? string.Empty;
    public string DecisionState
    {
        get
        {
            if (CurrentIndex < 0 || _review == null) return string.Empty;
            var decision = _review.GetDecision(CurrentIndex);
            return decision.Status switch
            {
                MuseReviewDecisionStatus.ApprovedNew      => $"Staged new item: {decision.ItemTitle}"
              , MuseReviewDecisionStatus.ApprovedExisting => $"Staged existing item: {decision.ItemTitle}"
              , MuseReviewDecisionStatus.Skipped          => "Skipped · choose a match to approve instead"
              , _                                        => "Not reviewed"
            };
        }
    }
    public string MatchTitle => SelectedExisting?.ImportReviewDisplay ?? SelectedProvider?.Display ?? "Choose a match";
    public string MatchEffect
    {
        get
        {
            if (SelectedExisting != null) return "Link to your existing WatchList item. Personal information stays unchanged.";
            return SelectedProvider != null
                ? "Add a new WatchList item. Its category applies to every approved row for this identity."
                : "Open Change match to choose an identity. Your approval is required.";
        }
    }
    public bool NeedsCategory => SelectedExisting == null && SelectedProvider != null;
    public bool HasApprovalFeedback => !string.IsNullOrWhiteSpace(ApprovalFeedback);
    public bool HasApprovalBlockReason => ApprovalBlockReason.Length > 0;
    public bool ShowSearchAction => _searchCancellation != null || _suggestionSession == null ||
                                   _suggestionSession.Completed.Count < Recommendations.Count ||
                                   _suggestionSession.Completed.Values.Any(result => result.Error != null);
    public bool CanApprove => !IsBusy && ApprovalBlockReason.Length == 0;
    public string ApprovalBlockReason
    {
        get
        {
            if (!_selectedRowCanBeReviewed) return "Saved item information is unavailable. Try revisiting this recommendation.";
            if (SelectedRecommendation?.SourceUrls.Count == 0) return "No source URL. Skip this recommendation or correct the file.";
            if (SelectedExisting?.IsDeleted == true) return "A deleted item cannot be approved. Choose another match.";
            if (SelectedExisting == null && SelectedProvider == null) return "Choose a match before approving.";
            if (NeedsCategory && (string.IsNullOrWhiteSpace(SelectedCategory) || !ImportCategories.Contains(SelectedCategory)))
                return "Choose a category for the new item.";
            return string.Empty;
        }
    }
    public bool HasPrevious => CurrentIndex > 0;
    public bool HasNext => CurrentIndex >= 0 && CurrentIndex < Recommendations.Count - 1;
    public string SearchAction => _searchCancellation == null ? "Resume searches" : "Stop searches";
    public string SearchProgress
    {
        get
        {
            var completed = _suggestionSession?.Completed.Values.ToList() ?? [];
            var problems = completed.Count(result => result.Error != null || result.Candidates.Count == 0);
            var state = _searchCancellation != null ? "Searching" : completed.Count == Recommendations.Count && completed.All(result => result.Error == null) ? "Finished" : "Paused";
            return $"{state} · {completed.Count}/{Recommendations.Count} searched · {problems} need attention";
        }
    }
    public string RowSearchStatus
    {
        get
        {
            if (CurrentIndex >= 0 && _correctedResults.ContainsKey(CurrentIndex)) return $"{ProviderMatches.Count} corrected search results.";
            if (SelectedRecommendation == null || _suggestionSession == null || !_suggestionSession.Completed.TryGetValue(SelectedRecommendation, out var result))
                return _searchCancellation != null ? "Suggestions are loading. You can move to another recommendation." : "Search paused. Resume to find suggestions.";
            if (result.Error == null && (SelectedExisting != null || SelectedProvider != null))
                return "Check the suggested identity before approving. Alternatives are available in Change match.";
            return result.Error ?? (result.Candidates.Count == 0 ? "No provider match found. Change the search or skip this recommendation."
                : result.Candidates.Count == 1 ? "One suggestion. Check the identity before approving." : $"{result.Candidates.Count} suggestions. Choose the correct identity in Change match.");
        }
    }
    public string ReviewProgress
    {
        get
        {
            var summary = _review?.GetSummary();
            return summary == null ? string.Empty : $"{summary.ApprovedRows} approved · {summary.SkippedRows} skipped · {summary.PendingRows} remaining";
        }
    }
    public string SummaryCounts
    {
        get
        {
            var summary = _review?.GetSummary();
            return summary == null ? string.Empty : $"Proposed: {summary.NewItems} unique new items\n{summary.ExistingItemRows} rows link existing items\n{summary.SourceLinks} source links from {summary.ApprovedRows} approved rows\n{summary.SkippedRows} skipped · {summary.PendingRows} still need a decision";
        }
    }
    public bool HasPending => (_review?.GetSummary().PendingRows ?? 0) > 0;
    public bool CanImport => !IsBusy && _review != null && _review.GetSummary() is { PendingRows: 0, ApprovedRows: > 0 };
    public string ImportAction => $"Import {_review?.ApprovedCount ?? 0} approved recommendations";
    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }
    private int CurrentIndex => SelectedRecommendation == null ? -1 : Recommendations.IndexOf(SelectedRecommendation);

    private void SetWizardStep(int step)
    {
        if (IsBusy) return;
        _wizardStep = step;
        RefreshWizard();
    }

    private void SaveSelection()
    {
        if (_restoringSelection || CurrentIndex < 0) return;
        _rowSelections[CurrentIndex] = new MuseReviewSelection(SelectedExisting, SelectedProvider, SelectedCategory);
    }

    private void RestoreSelection()
    {
        _restoringSelection = true;
        if (_rowSelections.TryGetValue(CurrentIndex, out var selection))
        {
            SelectedExisting = selection.Existing;
            SelectedProvider = selection.Provider;
            SelectedCategory = selection.Category;
        }
        else if (ExistingMatches.Count == 1) SelectedExisting = ExistingMatches[0];
        else if (ExistingMatches.Count == 0 && SelectedRecommendation != null)
            SelectedProvider = new MuseSuggestedMatchSelector().SelectUnambiguous(SelectedRecommendation, ProviderMatches);
        _restoringSelection = false;
        SearchQuery = SelectedRecommendation?.Title ?? string.Empty;
        RefreshWizard();
    }

    private void SynchronizeApprovedCategories()
    {
        if (_review == null || CurrentIndex < 0) return;
        var current = _review.GetDecision(CurrentIndex);
        foreach (var index in _rowSelections.Keys.ToList())
        {
            var decision = _review.GetDecision(index);
            if (decision.Status == MuseReviewDecisionStatus.ApprovedNew && decision.ItemId == current.ItemId)
                _rowSelections[index] = _rowSelections[index] with { Category = decision.Category };
        }
        SelectedCategory = current.Category;
    }

    private void RefreshWizard()
    {
        foreach (var name in new[] { nameof(WizardStep), nameof(IsFileStep), nameof(IsReviewStep), nameof(IsSummaryStep), nameof(IsResultStep)
                                 , nameof(HasFile), nameof(CanChangeReview), nameof(StepHeading), nameof(FileName), nameof(FileInformation)
                                 , nameof(RecommendationPosition), nameof(RecommendationTitle), nameof(RecommendationDescription), nameof(DecisionState)
                                 , nameof(MatchTitle), nameof(MatchEffect), nameof(NeedsCategory), nameof(CanApprove), nameof(ApprovalBlockReason)
                                 , nameof(HasApprovalFeedback), nameof(HasApprovalBlockReason), nameof(ShowSearchAction)
                                 , nameof(HasPrevious), nameof(HasNext), nameof(SearchAction), nameof(SearchProgress), nameof(RowSearchStatus)
                                 , nameof(ReviewProgress), nameof(SummaryCounts), nameof(HasPending), nameof(CanImport), nameof(ImportAction) })
            OnPropertyChanged(name);
    }

    [RelayCommand]
    private void BeginReview()
    {
        if (!HasFile) return;
        SelectedRecommendation ??= Recommendations[0];
        SetWizardStep(2);
    }

    [RelayCommand]
    private void PreviousRecommendation()
    {
        if (HasPrevious) SelectedRecommendation = Recommendations[CurrentIndex - 1];
    }

    [RelayCommand]
    private void NextRecommendation()
    {
        if (HasNext) SelectedRecommendation = Recommendations[CurrentIndex + 1];
        else ShowSummary();
    }

    [RelayCommand]
    private void ApproveAndNext()
    {
        if (!CanApprove) return;
        var approved = SelectedExisting != null ? ApproveExisting() : ApproveProvider();
        if (approved) NextRecommendation();
    }

    [RelayCommand]
    private void SkipAndNext()
    {
        if (_review == null || CurrentIndex < 0 || IsBusy) return;
        _review.Skip(CurrentIndex);
        RefreshWizard();
        NextRecommendation();
    }

    [RelayCommand]
    private void ShowSummary()
    {
        if (_review == null) return;
        StopSuggestions();
        RefreshReviewRows();
        SetWizardStep(3);
    }

    public void RefreshReviewRows()
    {
        if (_review == null) return;
        ReviewRows.Clear();
        for (var index = 0; index < Recommendations.Count; index++)
        {
            var decision = _review.GetDecision(index);
            var state = decision.Status switch
            {
                MuseReviewDecisionStatus.ApprovedNew => "New item"
              , MuseReviewDecisionStatus.ApprovedExisting => "Existing item link"
              , MuseReviewDecisionStatus.Skipped => "Skipped"
              , _ => "Pending"
            };
            if (decision.Status == MuseReviewDecisionStatus.Pending && _suggestionSession != null &&
                _suggestionSession.Completed.TryGetValue(Recommendations[index], out var result) &&
                (result.Error != null || result.Candidates.Count != 1)) state = "Pending · needs attention";
            ReviewRows.Add(new MuseReviewRow(index, Recommendations[index].Title, state, decision.ItemTitle.Length == 0 ? string.Empty
                : $"{decision.ItemTitle} · {decision.Provider} {decision.ProviderId} · {decision.MediaType} · {decision.Category}"));
        }
    }

    public void ReviewRow(int index)
    {
        if (IsBusy || index < 0 || index >= Recommendations.Count) return;
        SelectedRecommendation = Recommendations[index];
        SetWizardStep(2);
    }

    public void SkipRemaining()
    {
        if (IsBusy) return;
        _review?.SkipPending();
        ShowSummary();
    }

    [RelayCommand]
    private void ToggleSearches()
    {
        if (_searchCancellation != null) StopSuggestions();
        else _ = StartSuggestionsAsync();
    }
}
