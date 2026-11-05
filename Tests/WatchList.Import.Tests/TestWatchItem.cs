using SQLite;

namespace WatchList.Import.Tests;

[Table("WatchItems")]
public sealed class TestWatchItem
{
    [PrimaryKey]
    public Guid Id { get; set; }

    public bool IsDeleted { get; set; }
}
