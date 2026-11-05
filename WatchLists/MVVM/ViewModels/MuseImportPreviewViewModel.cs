using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchLists.MVVM.Models;
using WatchLists.Services;
using WatchLists.Services.Models;
using WatchLists.Services.MuseImport;

namespace WatchLists.MVVM.ViewModels;

public partial class MuseImportPreviewViewModel : ObservableObject
{
    private readonly WatchListService _watchListService;
    private readonly TmdbService _tmdbService;
    private MuseImportReview? _review;
    private bool _selectedRowCanBeReviewed;
    private readonly Dictionary<int, string> _approvalLabels = new();

    public ObservableCollection<MuseRecommendation> Recommendations { get; } = new();
    public ObservableCollection<WatchItem> ExistingMatches { get; } = new();
    public ObservableCollection<MovieSearchResult> ProviderMatches { get; } = new();

    private MuseRecommendation? _selectedRecommendation;
    private WatchItem? _selectedExisting;
    private MovieSearchResult? _selectedProvider;
    private string _status = "Select a Muse Markdown file to review. No data is changed by preview.";
    private string _details = string.Empty;
    private string _approvalSummary = "No rows approved.";
    private bool _isBusy;

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

    public MovieSearchResult? SelectedProvider
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

    public MuseImportPreviewViewModel(WatchListService watchListService, TmdbService tmdbService)
    {
        _watchListService = watchListService;
        _tmdbService      = tmdbService;
    }

    private void UpdateSelectedRecommendation(MuseRecommendation? value)
    {
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
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select Muse Markdown" });
            if (file == null) return;
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var result = new MuseMarkdownParser().Parse(await reader.ReadToEndAsync());
            Recommendations.Clear();
            foreach (var item in result.Recommendations) Recommendations.Add(item);
            _review = new MuseImportReview(result.Recommendations);
            _approvalLabels.Clear();
            UpdateApprovalSummary();
            SelectedRecommendation = null;
            Status = $"{result.Recommendations.Count} rows loaded; {result.Warnings.Count} warnings. " +
                     (result.Warnings.Count > 0 ? string.Join(" ", result.Warnings) : "Select a row to review.");
        }
        catch (Exception exception)
        {
            Status = $"Could not open file: {exception.Message}";
        }
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
            foreach (var candidate in result.Data?.Results ?? []) ProviderMatches.Add(candidate);
            Status = $"Found {ProviderMatches.Count} TMDB movie results. Verify the title and year before selecting.";
        }
        catch (Exception exception)
        {
            Status = $"Provider search failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ApproveExisting()
    {
        if (_review == null || SelectedRecommendation == null || SelectedExisting == null || !_selectedRowCanBeReviewed) return;
        try
        {
            _review.ApproveExisting(Recommendations.IndexOf(SelectedRecommendation), SelectedExisting);
            _approvalLabels[Recommendations.IndexOf(SelectedRecommendation)] = $"{SelectedRecommendation.Title} → existing {SelectedExisting.ImportReviewDisplay}";
            UpdateApprovalSummary();
            Status = $"Approved {_review.ApprovedCount} rows for later import. No data has been changed.";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    [RelayCommand]
    private void ApproveProvider()
    {
        if (_review == null || SelectedRecommendation == null || SelectedProvider == null || !_selectedRowCanBeReviewed) return;
        try
        {
            var item = new WatchItem
                       {
                           Title     = SelectedProvider.Title
                         , MovieId   = SelectedProvider.Id
                         , ApiSource = "TMDB"
                         , Type      = "Movie"
                         , Overview  = SelectedProvider.Overview ?? string.Empty
                         , PosterUrl = SelectedProvider.PosterUrl
                       };
            _review.ApproveNew(Recommendations.IndexOf(SelectedRecommendation), item);
            _approvalLabels[Recommendations.IndexOf(SelectedRecommendation)] = $"{SelectedRecommendation.Title} → {SelectedProvider.ImportReviewDisplay}";
            UpdateApprovalSummary();
            Status = $"Approved {_review.ApprovedCount} rows for later import. No data has been changed.";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
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
            Status = $"Approved {_review.ApprovedCount} rows for later import. No data has been changed.";
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
}
