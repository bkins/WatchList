using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using WatchLists.ExtensionMethods;
using WatchLists.Logger;
using WatchLists.Messaging;
using WatchLists.MVVM.Models;
using WatchLists.MVVM.Views;
using WatchLists.Services;
using WatchLists.Services.Enums;
using WatchLists.Services.Interfaces;
using WatchLists.Services.Models;
using WatchLists.Utilities;

namespace WatchLists.MVVM.ViewModels;

public partial class MovieDetailsViewModel : ObservableObject, IQueryAttributable
{
    private readonly IMovieDataAggregator _movieDataAggregator;
    private readonly WatchListService     _watchListService;
    private readonly SettingsService      _settingsService;

    [ObservableProperty] private MovieDetail? movieDetail;
    [ObservableProperty] private bool          _isAggregatedDataExpanded = false;

    public List<KeyValuePair<string, string>> AggregatedDataItems => MovieDetail?.AggregatedData?.ToList() ?? new();
    public bool HasAggregatedData => AggregatedDataItems != null && AggregatedDataItems.Count > 0;
    public bool HasNoAggregatedData => !HasAggregatedData;
    public string AggregatedDataExpandIcon => IsAggregatedDataExpanded ? "🔼 Collapse" : "🔽 Expand";
    public bool IsAggregatedDataContentVisible => HasAggregatedData && IsAggregatedDataExpanded;

    partial void OnIsAggregatedDataExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(AggregatedDataExpandIcon));
        OnPropertyChanged(nameof(IsAggregatedDataContentVisible));
    }

    [RelayCommand]
    private void ToggleAggregatedDataExpanded()
    {
        IsAggregatedDataExpanded = !IsAggregatedDataExpanded;
    }

    partial void OnMovieDetailChanged(MovieDetail? value)
    {
        OnPropertyChanged(nameof(AggregatedDataItems));
        OnPropertyChanged(nameof(HasAggregatedData));
        OnPropertyChanged(nameof(HasNoAggregatedData));
        OnPropertyChanged(nameof(IsAggregatedDataContentVisible));
    }

    public MovieDetailsViewModel (IMovieDataAggregator movieDataAggregator
                                , WatchListService     watchListService
                                , SettingsService      settingsService)
    {
        _movieDataAggregator = movieDataAggregator;
        _watchListService    = watchListService;
        _settingsService     = settingsService;
    }

    // This method is called when the page is navigated to with query parameters.
    public async void ApplyQueryAttributes (IDictionary<string, object> query)
    {
        if (query.TryGetValue("SearchResult", out var searchResultObj) && searchResultObj is MovieSearchResult searchResult)
        {
            MovieDetail = new MovieDetail
            {
                Id                 = searchResult.Id,
                Title              = searchResult.Title,
                Overview           = searchResult.Overview,
                PosterPath         = searchResult.PosterPath,
                StreamingProviders = searchResult.StreamingProviders,
                PrimarySourceApi   = searchResult.PrimarySourceApi,
                WebUrl             = searchResult.WebUrl,
                MediaType          = searchResult.MediaType,
                AggregatedData     = searchResult.AggregatedData != null ? new Dictionary<string, string>(searchResult.AggregatedData) : new()
            };

            var result = await ApiUtility.TryParseAndExecuteAsync<MovieDetail>(
                    searchResult.Id.ToString()
                  , _movieDataAggregator.GetMovieDetailsAsync
                  , "Movie ID");

            if (result.Data != null
             && result.Data.Title.HasValue()
             && result.Data.Title.EqualsIgnoreCase(searchResult.Title))
            {
                result.Data.StreamingProviders = searchResult.StreamingProviders;
                if (result.Data.WebUrl.HasNoValue()) result.Data.WebUrl = searchResult.WebUrl;
                if (result.Data.PrimarySourceApi.HasNoValue()) result.Data.PrimarySourceApi = searchResult.PrimarySourceApi;
                if (result.Data.MediaType.HasNoValue()) result.Data.MediaType = searchResult.MediaType;
                if (searchResult.AggregatedData != null)
                {
                    foreach (var kvp in searchResult.AggregatedData)
                    {
                        if (!result.Data.AggregatedData.ContainsKey(kvp.Key))
                        {
                            result.Data.AggregatedData[kvp.Key] = kvp.Value;
                        }
                    }
                }
                MovieDetail = result.Data;
            }
        }
        else if (query.TryGetValue("movieId", out var movieIdObj))
        {
            string movieIdStr = movieIdObj?.ToString() ?? "";

            var result = await ApiUtility.TryParseAndExecuteAsync<MovieDetail>(movieIdStr
                                                                             , _movieDataAggregator.GetMovieDetailsAsync
                                                                             , "Movie ID");

            if (result.Data != null)
            {
                MovieDetail = result.Data;
            }
        }
    }

    [RelayCommand]
    private async Task SelectMovie()
    {
        if (MovieDetail == null)
        {
            await FileLogger.WriteLogAsync("[SelectMovie] MovieDetail is null, aborting.");
            return;
        }

        var posterUrl = string.IsNullOrWhiteSpace(MovieDetail.PosterPath)
            ? string.Empty
            : MovieDetail.PosterPath.StartsWith("http")
                ? MovieDetail.PosterPath
                : $"https://image.tmdb.org/t/p/w500{MovieDetail.PosterPath}";

        var movie = new Movie
                    {
                        Id                 = MovieDetail.Id
                      , Title              = MovieDetail.Title
                      , Overview           = MovieDetail.Overview
                      , PosterPath         = posterUrl
                      , StreamingProviders = MovieDetail.StreamingProviders
                      , PrimarySourceApi   = MovieDetail.PrimarySourceApi
                      , WebUrl             = MovieDetail.WebUrl
                      , MediaType          = MovieDetail.MediaType
                      , AggregatedData     = MovieDetail.AggregatedData
                    };

        await FileLogger.WriteLogAsync($"[SelectMovie] Invoked. Selected Title: '{movie.Title}', Id: {movie.Id}");

        var stack = Shell.Current.Navigation.NavigationStack;
        bool cameFromEditPage = stack.Count >= 3 && stack[stack.Count - 3].GetType().Name.EqualsIgnoreCase(nameof(EditWatchItemPage));

        var existingItem = _watchListService.FindDuplicateItem(movie.Id, movie.PrimarySourceApi);
        if (existingItem != null)
        {
            await FileLogger.WriteLogAsync($"[SelectMovie] Duplicate detected for '{movie.Title}' (ID: {movie.Id}, ApiSource: {movie.PrimarySourceApi}). Category: '{existingItem.Category}'");

            bool navigateToExisting = await Shell.Current.DisplayAlert(
                "Item Already in WatchList",
                $"'{movie.Title}' is already in your WatchList under '{existingItem.Category}'.\n\nWould you like to open it?",
                "Open Item",
                "Cancel");

            if (navigateToExisting)
            {
                if (cameFromEditPage)
                {
                    await Shell.Current.GoToAsync($"../../EditWatchItemPage?watchItemId={existingItem.Id}");
                }
                else
                {
                    await Shell.Current.GoToAsync($"../EditWatchItemPage?watchItemId={existingItem.Id}");
                }
            }
            return;
        }

        if (cameFromEditPage)
        {
            await FileLogger.WriteLogAsync($"[SelectMovie] Flow: Came from EditWatchItemPage. Sending message & pop ../..");
            WeakReferenceMessenger.Default.Send(new MovieSelectedMessage(movie));
            await Shell.Current.GoToAsync("../..");
        }
        else
        {
            await FileLogger.WriteLogAsync($"[SelectMovie] Flow: Came from Discover tab. Creating WatchItem directly.");

            var categories = await _settingsService.GetOptionsAsync(SettingType.Categories);
            var defaultCategory = categories.FirstOrDefault() ?? "Currently Watching";

            var streamingService = movie.StreamingProviders.FirstOrDefault();
            if (streamingService.IsEmptyNullOrWhiteSpace())
            {
                var services = await _settingsService.GetOptionsAsync(SettingType.StreamingServices);
                streamingService = services.FirstOrDefault() ?? "Netflix";
            }
            else
            {
                var currentServices = await _settingsService.GetOptionsAsync(SettingType.StreamingServices);
                if (currentServices.DoesNotContain(streamingService))
                {
                    await _settingsService.AddOptionAsync(SettingType.StreamingServices, streamingService);
                }
            }

            var detectedType = movie.MediaType.HasValue() ? movie.MediaType : "Movie";
            var deepLink = DeepLinkUtility.GenerateDeepLink(streamingService, movie.Title, movie.WebUrl);

            var newItem = new WatchItem
            {
                Id                         = Guid.NewGuid(),
                MovieId                    = movie.Id,
                ApiSource                  = movie.PrimarySourceApi,
                Title                      = movie.Title,
                Overview                   = movie.Overview,
                PosterUrl                  = posterUrl,
                AvailableStreamingServices = string.Join(", ", movie.StreamingProviders),
                StreamingService           = streamingService,
                Category                   = defaultCategory,
                Type                       = detectedType,
                DeepLinkUri                = deepLink,
                IsWatched                  = false,
                LastUpdated                = DateTime.Now,
                AggregatedData             = movie.AggregatedData ?? new Dictionary<string, string>()
            };

            await _watchListService.SaveWatchItemAsync(newItem);
            await FileLogger.WriteLogAsync($"[SelectMovie] Successfully saved new WatchItem with ID '{newItem.Id}' to SQLite WatchList.");

            // Open EditWatchItemPage for the newly created item so the user can see and adjust fields if desired
            await Shell.Current.GoToAsync($"../EditWatchItemPage?watchItemId={newItem.Id}");
        }
    }
}
