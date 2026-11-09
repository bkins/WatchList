using SQLite;
using CommunityToolkit.Mvvm.ComponentModel;
using WatchLists.ExtensionMethods;

namespace WatchLists.MVVM.Models;

[Table("WatchItems")]
public class WatchItem : ObservableObject
{
    [PrimaryKey]
    public Guid     Id                         { get; set; } = Guid.NewGuid();

    [Indexed]
    public string   Title                      { get; set; } = string.Empty;

    public string   StreamingService           { get; set; } = string.Empty; // e.g., Netflix, Prime Video

    private string _category = string.Empty;

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public bool     IsWatched                  { get; set; }

    public bool     IsLiked                    { get; set; }

    [Indexed]
    public bool     IsDeleted                  { get; set; } = false;

    public string   DeepLinkUri                { get; set; } = string.Empty; // URL/URI to open the streaming service app

    [Indexed]
    public DateTime LastUpdated                { get; set; } = DateTime.UtcNow;

    public string   Type                       { get; set; } = string.Empty; // e.g., "Show", "Movie", "Mini-Series"

    public string   PreviousCategory           { get; set; } = string.Empty;

    [Indexed]
    public int      MovieId                    { get; set; }

    public string   ApiSource                  { get; set; } = string.Empty; // e.g. "TMDB", "TVMaze", "OMDB", "FM-DB"

    public string   Overview                   { get; set; } = string.Empty;

    public string   PosterUrl                  { get; set; } = string.Empty;

    public string   AvailableStreamingServices { get; set; } = string.Empty;

    public string   AggregatedDataJson         { get; set; } = string.Empty;

    [Ignore]
    public string ImportReviewDisplay => $"{Title} · {Type} · {ApiSource} {MovieId}" + (IsDeleted ? " (deleted)" : string.Empty);

    private bool _isSelected;

    [Ignore]
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

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
    public bool HasAvailableStreamingServices => AvailableStreamingServices.HasValue();

    [Ignore]
    public string AvailableStreamingServicesDisplay => HasAvailableStreamingServices
        ? "Available on: " + AvailableStreamingServices
        : "Streaming: Not checked (tap Refresh)";
}
