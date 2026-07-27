using SQLite;
using CommunityToolkit.Mvvm.ComponentModel;
using WatchLists.ExtensionMethods;

namespace WatchLists.MVVM.Models;

[Table("WatchItems")]
public class WatchItem : ObservableObject
{
    [PrimaryKey]
    public Guid     Id               { get; set; } = Guid.NewGuid();
    public string   Title            { get; set; }
    public string   StreamingService { get; set; } // e.g., Netflix, Prime Video
    public string   Category         { get; set; } // e.g., "Currently Watching", "Finished Watching"
    public bool     IsWatched        { get; set; }
    public bool     IsLiked          { get; set; }
    public string   DeepLinkUri      { get; set; } // URL/URI to open the streaming service app
    public DateTime LastUpdated      { get; set; } = DateTime.Now;
    public string   Type                       { get; set; } // e.g., "Show", "Movie", "Mini-Series"
    public string   PreviousCategory           { get; set; }
    public int      MovieId                    { get; set; }
    public string   ApiSource                  { get; set; } = string.Empty; // e.g. "TMDB", "TVMaze", "OMDB", "FM-DB"
    public string   Overview                   { get; set; } = string.Empty;
    public string   PosterUrl                  { get; set; } = string.Empty;
    public string   AvailableStreamingServices { get; set; } = string.Empty;
    public string   AggregatedDataJson         { get; set; } = string.Empty;

    [Ignore]
    public Dictionary<string, string> AggregatedData
    {
        get
        {
            if (AggregatedDataJson.HasNoValue()) return new Dictionary<string, string>();
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(AggregatedDataJson)
                       ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }
        set
        {
            AggregatedDataJson = value != null && value.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(value)
                : string.Empty;
            OnPropertyChanged();
        }
    }

    [Ignore]
    public bool HasAggregatedData => AggregatedData != null && AggregatedData.Count > 0;

    [Ignore]
    public bool HasAvailableStreamingServices => ! string.IsNullOrWhiteSpace(AvailableStreamingServices);

    [Ignore]
    public string AvailableStreamingServicesDisplay => HasAvailableStreamingServices
        ? "Available on: " + AvailableStreamingServices
        : "Streaming: Not checked (tap Refresh)";
}
