using SQLite;
using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;

namespace WatchLists.Services.ReliableSync;

public sealed class ReliableSyncStore
{
    private readonly string _databasePath;
    private readonly string _destination;

    public ReliableSyncStore(string databasePath, string destination)
    {
        _databasePath = databasePath;
        _destination  = destination;
    }

    internal TResult Transaction<TResult>(Func<SQLiteConnection, SyncDestinationState, TResult> work)
    {
        using var connection = new SQLiteConnection(_databasePath, SQLiteOpenFlags.ReadWrite);
        connection.BusyTimeout = TimeSpan.FromSeconds(5);
        connection.CreateTable<SyncStateRow>();
        connection.CreateTable<SyncRecoveryRow>();
        TResult result = default!;
        connection.RunInTransaction(() =>
        {
            // Acquire SQLite's writer reservation before reading local data on this connection.
            connection.Execute("UPDATE ReliableSyncState SET Data = Data WHERE Destination = ?", _destination);
            var row   = connection.Find<SyncStateRow>(_destination);
            var state = row is null ? SyncDestinationState.New : SyncJson.Read<SyncDestinationState>(row.Data);
            result = work(connection, state);
        });
        return result;
    }

    public SyncConflict? GetConflict() => Transaction((_, state) => state.Conflict);

    internal void Save(SQLiteConnection connection, SyncDestinationState state)
    {
        connection.InsertOrReplace(new SyncStateRow
                                   {
                                       Destination = _destination
                                     , Data        = SyncJson.Write(state)
                                   });
    }

    internal void Archive<TValue>(SQLiteConnection connection, string id, TValue value)
    {
        connection.InsertOrReplace(new SyncRecoveryRow
                                   {
                                       Id          = $"{_destination}:{id}"
                                     , Destination = _destination
                                     , Data        = SyncJson.Write(value)
                                   });
    }

    internal static SyncSnapshot ReadLocal(SQLiteConnection connection)
    {
        return new SyncSnapshot(connection.Table<WatchItem>().ToList().Select(SyncItem.From).ToArray()
                              , RecommendationStore.ReadExisting(connection).Select(SyncLink.From).ToArray()).Ordered();
    }

    internal static bool Apply(SQLiteConnection connection, SyncSnapshot local, SyncSnapshot merged)
    {
        SyncJson.Validate(merged);
        if (SyncJson.Equal(local, merged)) return false;

        var items = local.Items.ToDictionary(item => item.Id);
        foreach (var item in merged.Items)
        {
            if (items.GetValueOrDefault(item.Id) != item)
                connection.InsertOrReplace(item.ToWatchItem());
        }

        if (local.Links.Length > 0 || merged.Links.Length > 0)
        {
            _ = new RecommendationStore(connection);
            var links = local.Links.ToDictionary(link => (link.WatchItemId, link.SourceKey));
            var mergedKeys = merged.Links.Select(link => (link.WatchItemId, link.SourceKey)).ToHashSet();
            foreach (var removed in links.Values.Where(link => !mergedKeys.Contains((link.WatchItemId, link.SourceKey))))
                connection.Delete<MovieRecommendation>(removed.Id);
            foreach (var link in merged.Links)
            {
                if (!links.TryGetValue((link.WatchItemId, link.SourceKey), out var oldLink)
                    || SyncJson.Write(oldLink) != SyncJson.Write(link))
                    connection.InsertOrReplace(link.ToRecommendation());
            }
        }
        return true;
    }
}
