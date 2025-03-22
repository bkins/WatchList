using WatchLists.Services.Models;

namespace WatchLists.DataAccess.Interfaces;

public interface IMovieDataProvider
{
    bool                                            IsEnabled { get; }
    Task<AggregatedResult<MovieDetail?>>            GetMovieDetailsAsync (int   movieId);
    Task<AggregatedResult<MovieSearchResponse?>>    SearchMoviesAsync (string   query);
    Task<AggregatedResult<WatchProvidersResponse?>> GetWatchProvidersAsync (int movieId);
}
