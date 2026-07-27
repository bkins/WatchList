using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

public class TmdbFindResponse
{
    [JsonPropertyName("movie_results")]
    public List<MovieSearchResult>? MovieResults { get; set; }

    [JsonPropertyName("tv_results")]
    public List<TmdbTvDetail>? TvResults { get; set; }
}
