using SQLite;
using WatchLists.MVVM.Models;

namespace WatchList.Import.Tests;

public sealed class DisposableImportDatabase : IDisposable
{
    public string DirectoryPath { get; }
    public string DatabasePath { get; }
    public SQLiteConnection Connection { get; }

    public DisposableImportDatabase()
    {
        SQLitePCL.Batteries_V2.Init();
        DirectoryPath = Path.Combine(Path.GetTempPath(), "WatchListRegression", Guid.NewGuid().ToString("N"));
        DatabasePath  = Path.Combine(DirectoryPath, "watchlist.db");
        Directory.CreateDirectory(DirectoryPath);
        Connection = new SQLiteConnection(DatabasePath);
        Connection.CreateTable<WatchItem>();
    }

    public string CreateBackupPath() => Path.Combine(DirectoryPath, $"before-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        Connection.Dispose();
        Directory.Delete(DirectoryPath, true);
    }
}
