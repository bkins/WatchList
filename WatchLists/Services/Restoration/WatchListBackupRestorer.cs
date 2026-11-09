using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using SQLite;

namespace WatchLists.Services.Restoration;

internal sealed class WatchListBackupRestorer
{
    private const long MaximumBackupBytes = 256L * 1024 * 1024;
    private readonly string _databasePath;
    private readonly string _backupFolder;
    private readonly string _pendingPath;

    public WatchListBackupRestorer(string databasePath)
    {
        _databasePath = Path.GetFullPath(databasePath);
        _backupFolder = Path.Combine(Path.GetDirectoryName(_databasePath)!, "RestoreBackups");
        _pendingPath  = _databasePath + ".restore-pending";
    }

    public bool HasPendingRecovery => File.Exists(_pendingPath);

    public async Task<PreparedWatchListBackup> PrepareAsync(Stream source, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"WatchList-restore-{Guid.NewGuid():N}.db");
        try
        {
            await CopySelectionAsync(source, path, cancellationToken);
            return await Task.Run(() =>
            {
                WatchListBackupValidator.ValidateHeader(path);
                using var staged = Open(path, SQLiteOpenFlags.ReadOnly);
                var summary = WatchListBackupValidator.Validate(staged);
                return new PreparedWatchListBackup(path, _databasePath, Fingerprint(path), summary);
            }, cancellationToken);
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    public WatchListRestoreReceipt Restore(ref SQLiteConnection? live
                                           , PreparedWatchListBackup backup
                                           , Func<SQLiteConnection>? reopen = null)
    {
        if (backup.DatabasePath != _databasePath)
            throw new InvalidDataException("The selected backup was reviewed for a different database.");

        using var lease = AcquireRestoreLease();
        if (File.Exists(_pendingPath))
            throw new InvalidOperationException("Restart WatchList to finish the previous restore recovery.");

        if (!File.Exists(backup.Path) || Fingerprint(backup.Path) != backup.Fingerprint)
            throw new InvalidDataException("The selected backup changed. Choose and review it again.");
        WatchListBackupValidator.ValidateHeader(backup.Path);
        using var staged = Open(backup.Path, SQLiteOpenFlags.ReadOnly);
        var summary = WatchListBackupValidator.Validate(staged);
        if (summary != backup.Summary)
            throw new InvalidDataException("The selected backup changed. Choose and review it again.");

        var id = Guid.NewGuid();
        Directory.CreateDirectory(_backupFolder);
        var rollbackPath = RollbackPath(id);
        using (var rollback = Open(rollbackPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create))
        {
            CopyDatabase(live!, rollback);
            VerifyReopen(rollback);
        }
        WritePending(id);

        try
        {
            live!.Dispose();
            live = null;
            using (var destination = Open(_databasePath, SQLiteOpenFlags.ReadWrite))
                CopyDatabase(staged, destination);

            live = reopen is null ? Open(_databasePath, SQLiteOpenFlags.ReadWrite) : reopen();
            if (WatchListBackupValidator.Validate(live) != summary)
                throw new InvalidDataException("The reopened database did not match the reviewed backup.");

            File.Delete(_pendingPath);
            return new WatchListRestoreReceipt(summary, rollbackPath);
        }
        catch (Exception failure)
        {
            try
            {
                live?.Dispose();
                live = null;
                live = RestorePending();
                File.Delete(_pendingPath);
            }
            catch (Exception recoveryFailure)
            {
                live?.Dispose();
                live = null;
                throw new IOException("Restore could not finish. Restart WatchList to retry recovery from the saved rollback backup."
                                    , new AggregateException(failure, recoveryFailure));
            }
            throw new IOException("Restore failed. The previous WatchList was reopened from its rollback backup.", failure);
        }
    }

    public void RecoverIfPending()
    {
        if (!File.Exists(_pendingPath)) return;
        using var lease = AcquireRestoreLease();
        if (!File.Exists(_pendingPath)) return;
        using var recovered = RestorePending();
        File.Delete(_pendingPath);
    }

    private SQLiteConnection RestorePending()
    {
        if (!Guid.TryParseExact(File.ReadAllText(_pendingPath), "N", out var id))
            throw new InvalidDataException("The pending WatchList restore marker is invalid.");
        using var rollback = Open(RollbackPath(id), SQLiteOpenFlags.ReadOnly);
        VerifyReopen(rollback);
        using (var destination = Open(_databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create))
            CopyDatabase(rollback, destination);

        var reopened = Open(_databasePath, SQLiteOpenFlags.ReadWrite);
        try
        {
            VerifyReopen(reopened);
            return reopened;
        }
        catch
        {
            reopened.Dispose();
            throw;
        }
    }

    private FileStream AcquireRestoreLease()
        => new(_databasePath + ".restore-lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    private string RollbackPath(Guid id) => Path.Combine(_backupFolder, $"watchlist-before-restore-{id:N}.db");

    private void WritePending(Guid id)
    {
        var temporary = _pendingPath + "." + id.ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(Encoding.ASCII.GetBytes(id.ToString("N")));
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, _pendingPath);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static void VerifyReopen(SQLiteConnection connection)
    {
        if (connection.ExecuteScalar<string>("PRAGMA integrity_check") != "ok")
            throw new InvalidDataException("The rollback copy could not be verified.");
        _ = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM WatchItems");
    }

    private static SQLiteConnection Open(string path, SQLiteOpenFlags flags)
        => new(path, flags) { BusyTimeout = TimeSpan.FromSeconds(5) };

    private static string Fingerprint(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void CopyDatabase(SQLiteConnection source, SQLiteConnection destination)
    {
        var backup = SQLite3.BackupInit(destination.Handle, "main", source.Handle, "main");
        if (backup is null || backup.IsInvalid)
            throw new IOException("SQLite could not open a backup transaction.");

        SQLite3.Result step;
        SQLite3.Result finish;
        try
        {
            step = SQLite3.BackupStep(backup, -1);
        }
        finally
        {
            finish = SQLite3.BackupFinish(backup);
        }
        if (step != SQLite3.Result.Done || finish != SQLite3.Result.OK)
            throw new IOException($"SQLite backup transaction did not complete ({step}, {finish}).");
    }

    private static async Task CopySelectionAsync(Stream source, string path, CancellationToken cancellationToken)
    {
        await using var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(), cancellationToken)) != 0)
            {
                total += read;
                if (total > MaximumBackupBytes)
                    throw new InvalidDataException("The backup exceeds the 256 MB restore limit.");
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }
}
