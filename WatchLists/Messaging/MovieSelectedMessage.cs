using WatchLists.Services.Models;

namespace WatchLists.Messaging;

public class MovieSelectedMessage
{
    public Movie SelectedMovie { get; }

    public MovieSelectedMessage(Movie selectedMovie)
    {
        SelectedMovie = selectedMovie;
    }
}
