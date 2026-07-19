using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WatchLists.Logger;
using WatchLists.Messaging;
using WatchLists.MVVM.Views;
using WatchLists.Services;
using WatchLists.Services.Models;
using WatchLists.Utilities;

namespace WatchLists.MVVM.ViewModels;

public partial class MovieDetailsViewModel : ObservableObject, IQueryAttributable
{
    private readonly TmdbService _tmdbService;

    [ObservableProperty] private MovieDetail? movieDetail;

    public MovieDetailsViewModel (TmdbService tmdbService)
    {
        _tmdbService = tmdbService;
    }

    // This method is called when the page is navigated to with query parameters.
    public async void ApplyQueryAttributes (IDictionary<string, object> query)
    {
        if (query.TryGetValue("movieId"
                            , out object movieIdObj))
        {
            // Convert the query parameter to a string.
            string movieIdStr = movieIdObj?.ToString() ?? "";

            // Use the ApiUtility to validate and execute the API call.
            var result = await ApiUtility.TryParseAndExecuteAsync<MovieDetail>(
                    movieIdStr
                  , _tmdbService.GetMovieDetailsAsync
                  , "Movie ID");

            // If data is returned, assign it; otherwise, handle error/diagnostics as needed.
            if (result.Data != null)
            {
                MovieDetail = result.Data;
            }
            else
            {
                // Optionally, log or display the diagnostic error:
                // For example: MovieDetail = new MovieDetail { Title = result.Diagnostics["ApiTestViewModel"] };
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

        var movie = new Movie
        {
            Id         = MovieDetail.Id,
            Title      = MovieDetail.Title,
            Overview   = MovieDetail.Overview,
            PosterPath = MovieDetail.PosterPath ?? string.Empty
        };

        await FileLogger.WriteLogAsync($"[SelectMovie] Invoked. Selected Title: '{movie.Title}', Id: {movie.Id}");

        // Send the message to populated fields in EditWatchItemViewModel using modern WeakReferenceMessenger
        WeakReferenceMessenger.Default.Send(new MovieSelectedMessage(movie));

        await FileLogger.WriteLogAsync("[SelectMovie] Message sent. Navigating back...");

        // Go back two levels to EditWatchItemPage (from MovieDetailsPage -> SearchPage -> EditWatchItemPage)
        await Shell.Current.GoToAsync("../..");
    }
}
