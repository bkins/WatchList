using SQLite;

namespace WatchLists.Services.MuseImport;

public sealed class MovieRecommendation
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed(Name = "IX_Recommendation_Item_Source", Order = 1, Unique = true)]
    public Guid WatchItemId { get; set; }

    [Indexed(Name = "IX_Recommendation_Item_Source", Order = 2, Unique = true)]
    public string SourceKey { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;
    public string DescriptionsJson { get; set; } = "[]";
    public DateTime FirstImportedUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
}
