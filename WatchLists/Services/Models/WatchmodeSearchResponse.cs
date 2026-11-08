using System.Text.Json.Serialization;

namespace WatchLists.Services.Models;

internal sealed class WatchmodeSearchResponse
{
    [JsonPropertyName("title_results")]
    public List<WatchmodeTitleResult>? TitleResults { get; set; }
}
