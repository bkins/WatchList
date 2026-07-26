using SQLite;
using CommunityToolkit.Mvvm.ComponentModel;

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
    public string   Overview                   { get; set; } = string.Empty;
    public string   PosterUrl                  { get; set; } = string.Empty;
    public string   AvailableStreamingServices { get; set; } = string.Empty;

    [Ignore]
    public bool HasAvailableStreamingServices => ! string.IsNullOrWhiteSpace(AvailableStreamingServices);

    [Ignore]
    public string AvailableStreamingServicesDisplay => HasAvailableStreamingServices
        ? "Available on: " + AvailableStreamingServices
        : string.Empty;
}
