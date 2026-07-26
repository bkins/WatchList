using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using WatchLists.ExtensionMethods;
using WatchLists.Logger;
using WatchLists.Messaging;
using WatchLists.MVVM.Views;
using WatchLists.Services;
using WatchLists.Services.Models;
using WatchLists.Utilities;

using WatchLists.Services.Interfaces;

namespace WatchLists.MVVM.ViewModels;

public partial class MovieDetailsViewModel : ObservableObject, IQueryAttributable
{
    private readonly IMovieDataAggregator _movieDataAggregator;

    [ObservableProperty] private MovieDetail? movieDetail;

    public MovieDetailsViewModel (IMovieDataAggregator movieDataAggregator)
    {
        _movieDataAggregator = movieDataAggregator;
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
                StreamingProviders = searchResult.StreamingProviders
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
                    };

        await FileLogger.WriteLogAsync($"[SelectMovie] Invoked. Selected Title: '{movie.Title}', Id: {movie.Id}");

        // Send the message to populated fields in EditWatchItemViewModel using modern WeakReferenceMessenger
        WeakReferenceMessenger.Default.Send(new MovieSelectedMessage(movie));

        await FileLogger.WriteLogAsync("[SelectMovie] Message sent. Navigating back...");

        // Go back two levels to EditWatchItemPage (from MovieDetailsPage -> SearchPage -> EditWatchItemPage)
        await Shell.Current.GoToAsync("../..");
    }
}
