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
    private readonly Dictionary<int, string> _approvalLabels = new();

    public ObservableCollection<MuseRecommendation> Recommendations { get; } = new();
    public ObservableCollection<WatchItem> ExistingMatches { get; } = new();
    public ObservableCollection<MuseProviderCandidate> ProviderMatches { get; } = new();
    public ObservableCollection<string> ImportCategories { get; } = new();

    private MuseRecommendation? _selectedRecommendation;
    private WatchItem? _selectedExisting;
    private MuseProviderCandidate? _selectedProvider;
    private string _status = "Select a Muse Markdown file to review. No data is changed by preview.";
    private string _details = string.Empty;
    private string _approvalSummary = "No rows approved.";
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
        set => SetProperty(ref _selectedExisting, value);
    }

    public MuseProviderCandidate? SelectedProvider
    {
        get => _selectedProvider;
        set => SetProperty(ref _selectedProvider, value);
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
        set => SetProperty(ref _isBusy, value);
    }

    public string ApprovalSummary
    {
        get => _approvalSummary;
        set => SetProperty(ref _approvalSummary, value);
    }

    public string ApprovalFeedback
    {
        get => _approvalFeedback;
        set => SetProperty(ref _approvalFeedback, value);
    }

    private void ShowApprovalFeedback(string message)
    {
        ApprovalFeedback = message;
        Status = message;
    }
    public string? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
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
            ImportCategories.Clear();
            foreach (var category in categories.Where(category => !string.Equals(category, watchedCategory, StringComparison.OrdinalIgnoreCase)))
            {
                ImportCategories.Add(category);
            }
            SelectedCategory = ImportCategories.FirstOrDefault(category => string.Equals(category, "Consider Watching", StringComparison.OrdinalIgnoreCase));
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
        ApprovalFeedback = string.Empty;
        _selectedRowCanBeReviewed = false;
        ExistingMatches.Clear();
        ProviderMatches.Clear();
        SelectedExisting = null;
        SelectedProvider = null;
        if (value == null)
        {
            Details = string.Empty;
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
            Recommendations.Clear();
            foreach (var item in result.Recommendations) Recommendations.Add(item);
            _suggestionSession = new MuseSuggestionSession(result.Recommendations);
            _providerSearch = new MuseProviderSearch();
            _review = _suggestionSession.Review;
            _approvalLabels.Clear();
            UpdateApprovalSummary();
            SelectedRecommendation = null;
            Status = $"{result.Recommendations.Count} rows loaded; {result.Warnings.Count} warnings. " +
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
        try
        {
            await session.SearchPendingAsync(SearchProvidersAsync, result => MainThread.BeginInvokeOnMainThread(() =>
            {
                if (ReferenceEquals(session, _suggestionSession) && ReferenceEquals(result.Recommendation, SelectedRecommendation))
                    ShowSavedSuggestions(result.Recommendation);
            }), cancellation.Token);
            if (ReferenceEquals(session, _suggestionSession))
            {
                var failedCount = session.Completed.Values.Count(result => result.Error != null);
                Status = $"Suggestion searches finished; {failedCount} rows have provider failures. Review and approve each match.";
            }
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(session, _suggestionSession)) Status = "Searches stopped. Completed results and approvals remain; resume to retry pending rows.";
        }
        catch (Exception)
        {
            if (ReferenceEquals(session, _suggestionSession)) Status = "Suggestion searches failed. Completed results and approvals remain.";
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation)) _searchCancellation = null;
            cancellation.Dispose();
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
        if (_suggestionSession == null || !_suggestionSession.Completed.TryGetValue(row, out var result)) return;
        ProviderMatches.Clear();
        foreach (var candidate in result.Candidates) ProviderMatches.Add(candidate);
        Status = result.Error ?? $"{result.Candidates.Count} suggestions. Verify title, date and type; approval is required.";
    }

    [RelayCommand]
    private void StopSuggestions()
    {
        _searchCancellation?.Cancel();
    }

    public void AbandonReview()
    {
        _searchCancellation?.Cancel();
        _suggestionSession = null;
        _providerSearch = new MuseProviderSearch();
        _review = null;
        Recommendations.Clear();
        SelectedRecommendation = null;
        _approvalLabels.Clear();
        UpdateApprovalSummary();
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
            var result = await _tmdbService.SearchMoviesAsync(row.Title);
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
            var results = await _tvMazeSearch.SearchAsync(row.Title);
            if (!ReferenceEquals(row, SelectedRecommendation)) return;
            ProviderMatches.Clear();
            foreach (var candidate in results) ProviderMatches.Add(candidate);
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

    [RelayCommand]
    private void ApproveExisting()
    {
        var message = MuseApprovalReadiness.GetMessage(_review != null, SelectedRecommendation != null
                                                    , _selectedRowCanBeReviewed, SelectedExisting != null, true);
        if (message != null)
        {
            ShowApprovalFeedback(message);
            return;
        }
        var review = _review!;
        var selectedRecommendation = SelectedRecommendation!;
        var selectedExisting = SelectedExisting!;
        try
        {
            review.ApproveExisting(Recommendations.IndexOf(selectedRecommendation), selectedExisting);
            _approvalLabels[Recommendations.IndexOf(selectedRecommendation)] = $"{selectedRecommendation.Title} → existing {selectedExisting.ImportReviewDisplay}";
            UpdateApprovalSummary();
            ShowApprovalFeedback($"Approved {review.ApprovedCount} {(review.ApprovedCount == 1 ? "row" : "rows")}. Scroll down and tap Import approved rows when ready.");
        }
        catch (Exception exception)
        {
            ShowApprovalFeedback(exception.Message);
        }
    }

    [RelayCommand]
    private void ApproveProvider()
    {
        var message = MuseApprovalReadiness.GetMessage(_review != null, SelectedRecommendation != null
                                                    , _selectedRowCanBeReviewed, SelectedProvider != null, false);
        if (message != null)
        {
            ShowApprovalFeedback(message);
            return;
        }
        var review = _review!;
        var selectedRecommendation = SelectedRecommendation!;
        var selectedProvider = SelectedProvider!;
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedCategory) || !ImportCategories.Contains(SelectedCategory))
            {
                ShowApprovalFeedback("Choose a category for the new item before approving it.");
                return;
            }
            var existing = _watchListService.GetItemsForImportReview()
                                            .Where(item => item.MovieId == selectedProvider.Id &&
                                                           string.Equals(item.ApiSource, selectedProvider.Provider, StringComparison.OrdinalIgnoreCase))
                                            .ToList();
            if (existing.Count > 0)
            {
                ShowApprovalFeedback("This title is already in your WatchList. Choose it under Already in your WatchList and approve it there. Deleted items cannot be imported.");
                foreach (var match in existing.Where(item => !ExistingMatches.Any(candidate => candidate.Id == item.Id))) ExistingMatches.Add(match);
                return;
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
            _approvalLabels[Recommendations.IndexOf(selectedRecommendation)] = $"{selectedRecommendation.Title} → {selectedProvider.Display} · {SelectedCategory}";
            UpdateApprovalSummary();
            ShowApprovalFeedback($"Approved {review.ApprovedCount} {(review.ApprovedCount == 1 ? "row" : "rows")}. Scroll down and tap Import approved rows when ready.");
        }
        catch (Exception exception)
        {
            ShowApprovalFeedback(exception.Message);
        }
    }

    [RelayCommand]
    private void ClearApproval()
    {
        if (_review == null || SelectedRecommendation == null) return;
        var index = Recommendations.IndexOf(SelectedRecommendation);
        try
        {
            _review.ClearApproval(index);
            _approvalLabels.Remove(index);
            UpdateApprovalSummary();
            ShowApprovalFeedback($"Approval cleared. {_review.ApprovedCount} rows remain approved.");
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    private void UpdateApprovalSummary()
    {
        ApprovalSummary = _approvalLabels.Count == 0
            ? "No rows approved."
            : string.Join(Environment.NewLine, _approvalLabels.OrderBy(item => item.Key).Select(item => item.Value));
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
            return _review.CreateBatch();
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
            var receipt = await _watchListService.ApplyMuseImportAsync(batch);
            _suggestionSession?.ResetApprovals();
            _review = _suggestionSession?.Review;
            _approvalLabels.Clear();
            UpdateApprovalSummary();
            UpdateSelectedRecommendation(SelectedRecommendation);
            Status = $"Imported {receipt.NewItems} new items and {receipt.NewRecommendations} new sources. " +
                     $"Backup: {receipt.BackupPath}. Automatic sync remains off.";
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
