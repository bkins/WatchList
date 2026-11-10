using System.Text.Json;
using System.Text.RegularExpressions;
using SQLite;
using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;
using WatchLists.Services.ReliableSync;

namespace WatchLists.Services.Restoration;

internal static class WatchListBackupValidator
{
    public static void ValidateHeader(string path)
    {
        Span<byte> header = stackalloc byte[100];
        using var stream = File.OpenRead(path);
        if (stream.ReadAtLeast(header, header.Length, false) != header.Length
            || !header[..16].SequenceEqual("SQLite format 3\0"u8))
            throw new InvalidDataException("Choose a complete WatchList SQLite backup.");
        if (header[18] != 1 || header[19] != 1)
            throw new InvalidDataException("This file may need SQLite sidecars. Choose a backup made by WatchList.");
    }

    public static WatchListBackupSummary Validate(SQLiteConnection connection)
    {
        try
        {
            connection.Execute("PRAGMA trusted_schema = OFF");
            if (connection.ExecuteScalar<int>("PRAGMA user_version") != 0
                || connection.ExecuteScalar<int>("PRAGMA application_id") != 0)
                throw new InvalidDataException("This backup uses an unsupported database format.");

            VerifySchema(connection);
            if (connection.ExecuteScalar<string>("PRAGMA integrity_check") != "ok")
                throw new InvalidDataException("SQLite integrity validation failed. Choose another backup.");

            var snapshot = ReliableSyncStore.ReadLocal(connection);
            SyncJson.Validate(snapshot);

            var hasSyncState = VerifySyncState(connection);
            return new WatchListBackupSummary(snapshot.Items.Count(item => !item.IsDeleted)
                                            , snapshot.Items.Count(item => item.IsDeleted)
                                            , snapshot.Links.Count(link => link.IsRemoved != true)
                                            , hasSyncState);
        }
        catch (Exception exception) when (exception is SQLiteException or JsonException or ArgumentException or OverflowException or FormatException)
        {
            throw new InvalidDataException("This backup contains unreadable WatchList data or an unsupported schema.", exception);
        }
    }

    private static void VerifySchema(SQLiteConnection connection)
    {
        using var expected = new SQLiteConnection(":memory:");
        expected.CreateTable<WatchItem>();
        expected.CreateTable<MovieRecommendation>();
        expected.CreateTable<SyncStateRow>();
        expected.CreateTable<SyncRecoveryRow>();
        var knownObjects = Objects(expected).ToDictionary(item => item.Name, StringComparer.Ordinal);
        var objects = Objects(connection);

        if (!objects.Any(item => item.Type == "table" && item.Name == "WatchItems"))
            throw new InvalidDataException("This database does not contain a WatchList.");

        foreach (var item in objects)
        {
            if (!knownObjects.TryGetValue(item.Name, out var known)
                || item.Type != known.Type || item.TableName != known.TableName)
                throw new InvalidDataException("This backup contains a table, index, view or trigger that this version does not recognize.");

            if (item.Type == "table")
            {
                VerifyColumns(connection, expected, item.Name);
                if (item.Sql is null || !item.Sql.StartsWith("CREATE TABLE ", StringComparison.OrdinalIgnoreCase)
                    || Regex.IsMatch(item.Sql, @"\b(CHECK|COLLATE|REFERENCES|GENERATED|WITHOUT|STRICT|CONFLICT)\b", RegexOptions.IgnoreCase))
                    throw new InvalidDataException("This backup has unsupported table definitions.");
            }
            else if (NormalizeSql(item.Sql) != NormalizeSql(known.Sql))
            {
                throw new InvalidDataException("This backup has an unsupported index definition.");
            }
        }

        if (objects.Any(item => item.Name == "MovieRecommendation")
            && !objects.Any(item => item.Name == "IX_Recommendation_Item_Source"))
            throw new InvalidDataException("The recommendation identity index is missing.");
    }

    private static List<BackupSchemaObject> Objects(SQLiteConnection connection)
    {
        return connection.Query<BackupSchemaObject>(
            "SELECT type AS Type, name AS Name, tbl_name AS TableName, sql AS Sql FROM sqlite_schema");
    }

    private static string NormalizeSql(string? sql)
        => sql is null ? string.Empty : Regex.Replace(sql, "\\s+", string.Empty).ToUpperInvariant();

    private static void VerifyColumns(SQLiteConnection connection, SQLiteConnection expected, string table)
    {
        var query = $"PRAGMA table_xinfo(\"{table}\")";
        var actual = connection.Query<BackupColumn>(query).OrderBy(column => column.Name).ToArray();
        var known  = expected.Query<BackupColumn>(query).OrderBy(column => column.Name).ToArray();
        if (table == "MovieRecommendation" && actual.All(column => !IsEnhancedColumn(column.Name)))
            known = known.Where(column => !IsEnhancedColumn(column.Name)).ToArray();
        if (actual.Length != known.Length
            || actual.Zip(known).Any(pair => pair.First.Name != pair.Second.Name
                                         || !pair.First.Type.Equals(pair.Second.Type, StringComparison.OrdinalIgnoreCase)
                                         || pair.First.Pk != pair.Second.Pk || pair.First.Hidden != 0))
            throw new InvalidDataException("This backup has unsupported or missing WatchList fields.");

        foreach (var column in known)
        {
            var value = $"\"{column.Name}\"";
            var storage = column.Type.StartsWith("varchar", StringComparison.OrdinalIgnoreCase) ? "text" : "integer";
            var required = column.Pk != 0 || column.Name is "LastUpdated" or "FirstImportedUtc" or "LastSeenUtc"
                                                   or "IsWatched" or "IsLiked" or "IsDeleted";
            var invalid = required ? $"typeof({value}) != '{storage}'" : $"typeof({value}) NOT IN ('null','{storage}')";
            if (column.Name is "IsWatched" or "IsLiked" or "IsDeleted" or "IsPrimary" or "IsRemoved")
                invalid += $" OR {value} NOT IN (0,1)";
            if (connection.ExecuteScalar<int>($"SELECT EXISTS(SELECT 1 FROM \"{table}\" WHERE {invalid})") != 0)
                throw new InvalidDataException("This backup contains a value in an incompatible SQLite format.");
        }
    }

    private static bool IsEnhancedColumn(string name)
        => name is "OriginalMetadata" or "EditedMetadata" or "IsPrimary" or "IsRemoved";

    private static bool VerifySyncState(SQLiteConnection connection)
    {
        var stateCount = 0;
        if (HasTable(connection, "ReliableSyncState"))
        {
            var rows = connection.Table<SyncStateRow>().ToList();
            stateCount = rows.Count;
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Destination) || string.IsNullOrWhiteSpace(row.Data))
                    throw new InvalidDataException("This backup contains incomplete sync recovery state.");

                var state = SyncJson.Read<SyncDestinationState>(row.Data);
                if (state.Baseline is null)
                    throw new InvalidDataException("This backup contains an incomplete sync baseline.");
                SyncJson.Validate(state.Baseline);
                if (state.Pending is not null) ValidatePending(state.Pending);
                if (state.Conflict is not null) ValidateConflict(state.Conflict);
            }
        }

        if (!HasTable(connection, "ReliableSyncRecovery")) return stateCount > 0;

        var archives = connection.Table<SyncRecoveryRow>().ToList();
        foreach (var row in archives)
        {
            if (string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.Destination) || string.IsNullOrWhiteSpace(row.Data))
                throw new InvalidDataException("This backup contains incomplete sync recovery history.");

            using var document = JsonDocument.Parse(row.Data);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("This backup contains invalid sync recovery history.");
            if (document.RootElement.TryGetProperty("proposal", out _))
                ValidatePending(SyncJson.Read<SyncPending>(row.Data));
            else
                ValidateConflict(SyncJson.Read<SyncConflict>(row.Data));
        }
        return stateCount > 0 || archives.Count > 0;
    }

    private static bool HasTable(SQLiteConnection connection, string table)
        => connection.ExecuteScalar<int>("SELECT EXISTS(SELECT 1 FROM sqlite_schema WHERE type = 'table' AND name = ?)", table) != 0;

    private static void ValidatePending(SyncPending pending)
    {
        if (pending.Proposal is null || pending.LocalBefore is null || string.IsNullOrWhiteSpace(pending.Etag))
            throw new InvalidDataException("This backup contains an incomplete prepared sync write.");
        _ = pending.Proposal.Decode();
        SyncJson.Validate(pending.LocalBefore);
    }

    private static void ValidateConflict(SyncConflict conflict)
    {
        if (conflict.Id == Guid.Empty || conflict.ItemIds is null
            || conflict.Baseline is null || conflict.Local is null || conflict.Remote is null)
            throw new InvalidDataException("This backup contains an incomplete sync conflict.");
        SyncJson.Validate(conflict.Baseline);
        SyncJson.Validate(conflict.Local);
        SyncJson.Validate(conflict.Remote);
    }
}
