namespace WatchLists.MVVM.Models;

public class WatchItem
{
    public Guid     Id               { get; set; } = Guid.NewGuid();
    public string   Title            { get; set; }
    public string   StreamingService { get; set; } // e.g., Netflix, Prime Video
    public string   Category         { get; set; } // e.g., "Currently Watching", "Finished Watching"
    public bool     IsWatched        { get; set; }
    public bool     IsLiked          { get; set; }
    public string   DeepLinkUri      { get; set; } // URL/URI to open the streaming service app
    public DateTime LastUpdated      { get; set; } = DateTime.Now;
    public string   Type             { get; set; } // e.g., "Show", "Movie", "Mini-Series"
}
