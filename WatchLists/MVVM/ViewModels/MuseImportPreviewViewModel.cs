using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchLists.MVVM.Models;
using WatchLists.Services;
using WatchLists.Services.MuseImport;
using WatchLists.Services.Enums;

namespace WatchLists.MVVM.ViewModels;

public partial class MuseImportPreviewViewModel : ObservableObject
{
    private readonly WatchListService _watchListService;
    private readonly TmdbService _tmdbService;
    private readonly TvMazeImportSearch _tvMazeSearch;
    private readonly SettingsService _settingsService;
    private MuseImportReview? _review;
    private MuseSuggestionSession? _suggestionSession;
    private MuseProviderSearch _providerSearch = new();
    private CancellationTokenSource? _searchCancellation;
    private bool _selectedRowCanBeReviewed;
    private IReadOnlyList<MuseReviewGroup> _reviewGroups = [];

    public ObservableCollection<MuseRecommendation> Recommendations { get; } = new();
    public ObservableCollection<WatchItem> ExistingMatches { get; } = new();
    public ObservableCollection<MuseProviderCandidate> ProviderMatches { get; } = new();
    public ObservableCollection<string> ImportCategories { get; } = new();

    private MuseRecommendation? _selectedRecommendation;
    private WatchItem? _selectedExisting;
    private MuseProviderCandidate? _selectedProvider;
    private string _status = "Select a Muse Markdown file to review. No data is changed by preview.";
    private string _details = string.Empty;
    private string _approvalFeedback = string.Empty;
    private bool _isBusy;
    private string? _selectedCategory;

    public MuseRecommendation? SelectedRecommendation
    {
        get => _selectedRecommendation;
        set
        {
            if (SetProperty(ref _selectedRecommendation, value)) UpdateSelectedRecommendation(value);
        }
    }

    public WatchItem? SelectedExisting
    {
        get => _selectedExisting;
        set
        {
            if (!SetProperty(ref _selectedExisting, value)) return;
            if (value != null) SetProperty(ref _selectedProvider, null, nameof(SelectedProvider));
            SaveSelection();
            RefreshWizard();
        }
    }

    public MuseProviderCandidate? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (!SetProperty(ref _selectedProvider, value)) return;
            if (value != null) SetProperty(ref _selectedExisting, null, nameof(SelectedExisting));
            SaveSelection();
            RefreshWizard();
        }
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string Details
    {
        get => _details;
        set => SetProperty(ref _details, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value)) RefreshWizard();
        }
    }

    public string ApprovalFeedback
    {
        get => _approvalFeedback;
        set
        {
            if (SetProperty(ref _approvalFeedback, value)) OnPropertyChanged(nameof(HasApprovalFeedback));
        }
    }

    private void ShowApprovalFeedback(string message)
    {
        ApprovalFeedback = message;
        Status = message;
    }
    public string? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetProperty(ref _selectedCategory, value)) return;
            SaveSelection();
            RefreshWizard();
        }
    }

    public MuseImportPreviewViewModel(WatchListService watchListService
                                    , TmdbService tmdbService
                                    , TvMazeImportSearch tvMazeSearch
                                    , SettingsService settingsService)
    {
        _watchListService = watchListService;
        _tmdbService      = tmdbService;
        _tvMazeSearch     = tvMazeSearch;
        _settingsService  = settingsService;
    }

    public async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _settingsService.GetOptionsAsync(SettingType.Categories);
            var watchedCategory = await _settingsService.GetWatchedCategoryAsync();
            var selectedCategory = SelectedCategory;
            ImportCategories.Clear();
            foreach (var category in categories.Where(category => !string.Equals(category, watchedCategory, StringComparison.OrdinalIgnoreCase)))
            {
                ImportCategories.Add(category);
            }
            SelectedCategory = ImportCategories.Contains(selectedCategory ?? string.Empty)
                ? selectedCategory
                : ImportCategories.FirstOrDefault(category => string.Equals(category, "Consider Watching", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            ImportCategories.Clear();
            SelectedCategory = null;
            Status = "Could not load categories. New items cannot be approved until categories are available.";
        }
    }

    private void UpdateSelectedRecommendation(MuseRecommendation? value)
    {
        ShowOriginalEntries = false;
        _restoringSelection = true;
        ApprovalFeedback = string.Empty;
        _selectedRowCanBeReviewed = false;
        ExistingMatches.Clear();
        ProviderMatches.Clear();
        SelectedExisting = null;
        SelectedProvider = null;
        if (value == null)
        {
            Details = string.Empty;
            _restoringSelection = false;
            RefreshWizard();
            return;
        }

        ShowSavedSuggestions(value);

        try
        {
            var items = _watchListService.GetItemsForImportReview();
            var sources = _watchListService.GetRecommendationsForSync();
            var candidates = new MusePreviewBuilder().Build([value]
                , items.Select(item => new MuseExistingItem(item.Id, item.Title, item.IsDeleted, item.MovieId, item.ApiSource)));
            var ids = candidates[0].PossibleMatches.Select(item => item.Id).ToHashSet();
            foreach (var item in items.Where(item => ids.Contains(item.Id))) ExistingMatches.Add(item);

            var associations = new MuseSourceInspector().Find(value, sources, items);
            var savedDetails = associations.Count == 0
                ? "No saved associations for these source URLs."
                : string.Join(Environment.NewLine, associations.Select(association =>
                    $"{association.SourceUrl} → {association.ItemTitle}" + (association.IsDeleted ? " (deleted)" : string.Empty)));
            Details = $"{value.Section} · line {value.LineNumber}\n{value.Description}\nSources: {string.Join(", ", value.SourceUrls)}\n" +
                      $"Saved source associations (review identity):\n{savedDetails}";
            _selectedRowCanBeReviewed = true;
        }
        catch (Exception exception)
        {
            Details = "Could not read saved items or source associations. Approval is disabled for this row.";
            Status = $"Review unavailable: {exception.Message}";
        }
        RestoreSelection();
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        if (_suggestionSession != null)
        {
            Status = "End the current review before opening another file.";
            return;
        }
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select Muse Markdown" });
            if (file == null) return;
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var result = new MuseMarkdownParser().Parse(await reader.ReadToEndAsync());
            _reviewGroups = new MuseReviewGrouper().Group(result.Recommendations);
            var reviewRecommendations = _reviewGroups.Select(group => group.Representative).ToList();
            Recommendations.Clear();
            foreach (var item in reviewRecommendations) Recommendations.Add(item);
            _suggestionSession = new MuseSuggestionSession(reviewRecommendations);
            _providerSearch = new MuseProviderSearch();
            _review = _suggestionSession.Review;
            RefreshWizard();
            SelectedRecommendation = null;
            _fileName = file.FileName;
            _fileInformation = $"{result.Recommendations.Count} document entries · {Recommendations.Count} review items · " +
                               $"{result.Recommendations.Count - Recommendations.Count} repeated entries grouped · {result.Warnings.Count} warnings" +
                               (result.Warnings.Count > 0 ? "\n" + string.Join("\n", result.Warnings) : string.Empty);
            RefreshWizard();
            Status = $"{Recommendations.Count} review items loaded; {result.Warnings.Count} warnings. " +
                     (result.Warnings.Count > 0 ? string.Join(" ", result.Warnings) : "Select a row to review.");
            _ = StartSuggestionsAsync();
        }
        catch (Exception exception)
        {
            Status = $"Could not open file: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task StartSuggestionsAsync()
    {
        var session = _suggestionSession;
        if (session == null || _searchCancellation != null) return;
        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        RefreshWizard();
        try
        {
            await session.SearchPendingAsync(SearchProvidersAsync, result => MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!ReferenceEquals(session, _suggestionSession) || _wizardStep == 4) return;
                if (ReferenceEquals(result.Recommendation, SelectedRecommendation)) ShowSavedSuggestions(result.Recommendation);
                RefreshWizard();
            }), cancellation.Token);
            if (ReferenceEquals(session, _suggestionSession) && _wizardStep != 4)
            {
                var failedCount = session.Completed.Values.Count(result => result.Error != null);
                Status = $"Suggestion searches finished; {failedCount} rows have provider failures. Review and approve each match.";
            }
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(session, _suggestionSession) && _wizardStep != 4) Status = "Searches stopped. Completed results and approvals remain; resume to retry pending rows.";
        }
        catch (Exception)
        {
            if (ReferenceEquals(session, _suggestionSession) && _wizardStep != 4) Status = "Suggestion searches failed. Completed results and approvals remain.";
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation)) _searchCancellation = null;
            cancellation.Dispose();
            RefreshWizard();
        }
    }

    private async Task<IReadOnlyList<MuseProviderCandidate>> SearchProvidersAsync(string title, CancellationToken cancellationToken)
    {
        return await _providerSearch.SearchAsync(title
                                               , _tmdbService.SearchImportMoviesAsync
                                               , _tvMazeSearch.SearchAsync
                                               , cancellationToken);
    }

    private void ShowSavedSuggestions(MuseRecommendation row)
    {
        IReadOnlyList<MuseProviderCandidate> candidates;
        if (_correctedResults.TryGetValue(Recommendations.IndexOf(row), out var corrected)) candidates = corrected;
        else if (_suggestionSession != null && _suggestionSession.Completed.TryGetValue(row, out var result)) candidates = result.Candidates;
        else return;
        var previous = SelectedProvider;
        var wasRestoring = _restoringSelection;
        _restoringSelection = true;
        ProviderMatches.Clear();
        foreach (var candidate in candidates) ProviderMatches.Add(candidate);
        SelectedProvider = previous;
        if (previous == null && SelectedExisting == null && !_rowSelections.ContainsKey(CurrentIndex))
            SelectSuggestedMatch(row);
        _restoringSelection = wasRestoring;
        RefreshWizard();
    }

    [RelayCommand]
    private void StopSuggestions()
    {
        _searchCancellation?.Cancel();
    }

    public void AbandonReview()
    {
        _searchCancellation?.Cancel();
        _searchCancellation = null;
        _suggestionSession = null;
        _providerSearch = new MuseProviderSearch();
        _review = null;
        _reviewGroups = [];
        Recommendations.Clear();
        SelectedRecommendation = null;
        _rowSelections.Clear();
        _correctedResults.Clear();
        _fileName = string.Empty;
        _fileInformation = string.Empty;
        SetWizardStep(1);
        RefreshWizard();
        Status = "Review ended. Select a file to start another review.";
    }

    [RelayCommand]
    private async Task SearchProviderAsync()
    {
        if (SelectedRecommendation == null || IsBusy) return;
        var row = SelectedRecommendation;
        IsBusy = true;
        try
        {
            var result = await _tmdbService.SearchMoviesAsync(SearchQuery);
            if (!ReferenceEquals(row, SelectedRecommendation)) return;
            ProviderMatches.Clear();
            if (result.Data == null)
            {
                Status = "TMDB search did not return results. Check the connection and provider configuration.";
                return;
            }
            foreach (var candidate in result.Data.Results ?? [])
            {
                ProviderMatches.Add(new MuseProviderCandidate(candidate.Id, candidate.Title, candidate.ReleaseDate
                                                            , candidate.Overview ?? string.Empty, candidate.PosterUrl
                                                            , "TMDB", "Movie"));
            }
            _correctedResults[CurrentIndex] = ProviderMatches.ToList();
            RefreshWizard();
            Status = $"Found {ProviderMatches.Count} TMDB movie results. Verify the title and year before selecting.";
        }
        catch (Exception)
        {
            ProviderMatches.Clear();
            Status = "TMDB search failed. Check the connection and provider configuration.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchShowsAsync()
    {
        if (SelectedRecommendation == null || IsBusy) return;
        var row = SelectedRecommendation;
        IsBusy = true;
        try
        {
            var results = await _tvMazeSearch.SearchAsync(SearchQuery);
            if (!ReferenceEquals(row, SelectedRecommendation)) return;
            ProviderMatches.Clear();
            foreach (var candidate in results) ProviderMatches.Add(candidate);
            _correctedResults[CurrentIndex] = ProviderMatches.ToList();
            RefreshWizard();
            Status = $"Found {ProviderMatches.Count} TVMaze shows. Verify the title and premiere date before selecting.";
        }
        catch (Exception)
        {
            ProviderMatches.Clear();
            Status = "TVMaze search failed. Check the connection and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool ApproveExisting()
    {
        var message = MuseApprovalReadiness.GetMessage(_review != null, SelectedRecommendation != null
                                                    , _selectedRowCanBeReviewed, SelectedExisting != null, true);
        if (message != null)
        {
            ShowApprovalFeedback(message);
            return false;
        }
        var review = _review!;
        var selectedRecommendation = SelectedRecommendation!;
        var selectedExisting = SelectedExisting!;
        try
        {
            review.ApproveExisting(Recommendations.IndexOf(selectedRecommendation), selectedExisting);
            SaveSelection();
            RefreshWizard();
            ShowApprovalFeedback($"Approved {review.ApprovedCount} {(review.ApprovedCount == 1 ? "row" : "rows")}. Continue reviewing or open the summary when ready.");
            return true;
        }
        catch (Exception exception)
        {
            ShowApprovalFeedback(exception.Message);
        }
        return false;
    }

    private bool ApproveProvider()
    {
        var message = MuseApprovalReadiness.GetMessage(_review != null, SelectedRecommendation != null
                                                    , _selectedRowCanBeReviewed, SelectedProvider != null, false);
        if (message != null)
        {
            ShowApprovalFeedback(message);
            return false;
        }
        var review = _review!;
        var selectedRecommendation = SelectedRecommendation!;
        var selectedProvider = SelectedProvider!;
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedCategory) || !ImportCategories.Contains(SelectedCategory))
            {
                ShowApprovalFeedback("Choose a category for the new item before approving it.");
                return false;
            }
            var existing = _watchListService.GetItemsForImportReview()
                                            .Where(item => item.MovieId == selectedProvider.Id &&
                                                           string.Equals(item.ApiSource, selectedProvider.Provider, StringComparison.OrdinalIgnoreCase))
                                            .ToList();
            if (existing.Count > 0)
            {
                foreach (var match in existing.Where(item => !ExistingMatches.Any(candidate => candidate.Id == item.Id))) ExistingMatches.Add(match);
                if (existing.Count == 1 && !existing[0].IsDeleted)
                {
                    SelectedExisting = existing[0];
                    return ApproveExisting();
                }
                else ShowApprovalFeedback("This identity has existing or deleted matches. Open Change match and choose the saved item explicitly. Deleted items cannot be imported.");
                return false;
            }
            var item = new WatchItem
                       {
                           Title     = selectedProvider.Title
                         , MovieId   = selectedProvider.Id
                         , ApiSource = selectedProvider.Provider
                         , Type      = selectedProvider.MediaType
                         , Category  = SelectedCategory
                         , Overview  = selectedProvider.Overview ?? string.Empty
                         , PosterUrl = selectedProvider.PosterUrl
                       };
            review.ApproveNew(Recommendations.IndexOf(selectedRecommendation), item);
            SaveSelection();
            SynchronizeApprovedCategories();
            RefreshWizard();
            ShowApprovalFeedback($"Approved {review.ApprovedCount} {(review.ApprovedCount == 1 ? "row" : "rows")}. Continue reviewing or open the summary when ready.");
            return true;
        }
        catch (Exception exception)
        {
            ShowApprovalFeedback(exception.Message);
        }
        return false;
    }

    [RelayCommand]
    private void ClearApproval()
    {
        if (_review == null || SelectedRecommendation == null) return;
        var index = Recommendations.IndexOf(SelectedRecommendation);
        try
        {
            _review.ClearApproval(index);
            RefreshWizard();
            ShowApprovalFeedback($"Approval cleared. {_review.ApprovedCount} rows remain approved.");
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    public MuseImportBatch? PrepareImport()
    {
        if (_review == null)
        {
            Status = "Choose a Muse file and approve at least one row first.";
            return null;
        }
        if (IsBusy)
        {
            Status = "Please wait for the current operation to finish.";
            return null;
        }
        try
        {
            return _review.CreateFinalBatch();
        }
        catch (Exception exception)
        {
            Status = exception.Message;
            return null;
        }
    }

    public async Task ImportConfirmedAsync(MuseImportBatch batch)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var skippedRows = _review?.GetSummary().SkippedRows ?? 0;
            var receipt = await _watchListService.ApplyMuseImportAsync(batch);
            _importBackupPath = receipt.BackupPath;
            _wizardStep = 4;
            _searchCancellation?.Cancel();
            _suggestionSession?.ResetApprovals();
            _review = _suggestionSession?.Review;
            RefreshWizard();
            UpdateSelectedRecommendation(SelectedRecommendation);
            Status = $"Added {receipt.NewItems} new items and {receipt.NewRecommendations} source links. " +
                     $"{batch.Recommendations.Count - receipt.NewRecommendations} source-link proposals were already present or duplicates. " +
                     $"{skippedRows} recommendations skipped. Cloud sync remains paused.";
        }
        catch (Exception exception)
        {
            Status = $"Import did not complete: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
