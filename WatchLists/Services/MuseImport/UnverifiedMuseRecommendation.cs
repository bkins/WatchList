using SQLite;

namespace WatchLists.Services.MuseImport;

public sealed class UnverifiedMuseRecommendation
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed(Unique = true)]
    public string SourceKey { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public int? ReleaseYear { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public int SourceLineNumber { get; set; }
    public DateTime FirstImportedUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
}
