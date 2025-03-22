using WatchLists.Services.Models;

namespace WatchLists.Services.Interfaces;

public interface IMovieDataAggregator
{
    Task<AggregatedResult<MovieDetail>>            GetMovieDetailsAsync (int   movieId);
    Task<AggregatedResult<MovieSearchResponse>>    SearchMoviesAsync (string   query);
    Task<AggregatedResult<WatchProvidersResponse>> GetWatchProvidersAsync (int movieId);
    Task<AggregatedResult<MovieSearchResult>>      SearchMovieAsync (string    searchQuery);
}
