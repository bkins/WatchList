namespace WatchLists.Services.Restoration;

internal sealed class BackupSchemaObject
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string? Sql { get; set; }
}
