using System.Security.Cryptography;
using System.Text;

namespace WatchLists.Services.ReliableSync;

public sealed record FirebaseSyncProfile(string DatabaseUrl, string ApiKey, string Email, string Code, bool AutoSyncEnabled)
{
    public Uri Validate(bool allowEmulator)
    {
        if (!Uri.TryCreate(DatabaseUrl.Trim(), UriKind.Absolute, out var database) || database.AbsolutePath != "/"
            || database.UserInfo.Length > 0 || database.Fragment.Length > 0
            || string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(Email)
            || !Guid.TryParseExact(Code, "N", out _))
            throw new InvalidDataException("Enter the database root, Firebase API key, email and a 32-character pairing code.");

        if (database.Scheme == "https" && database.IsDefaultPort && database.Query.Length == 0
            && (database.Host.EndsWith(".firebaseio.com", StringComparison.OrdinalIgnoreCase)
                || database.Host.EndsWith(".firebasedatabase.app", StringComparison.OrdinalIgnoreCase)))
            return database;

        if (allowEmulator && database.Scheme == "http" && database.Host is "127.0.0.1" or "10.0.2.2"
            && database.Port == 9000 && database.Query == "?ns=demo-watchlist-default-rtdb")
            return database;

        throw new InvalidDataException("Use the HTTPS root of the provisioned Firebase Realtime Database.");
    }

    public string CredentialKey(Uri database)
        => "watchlist-sync-" + Hash($"{database.AbsoluteUri}\n{ApiKey}\n{Email.ToUpperInvariant()}");

    public string DestinationKey(Uri database, string userId)
        => Hash($"{database.AbsoluteUri}\n{userId}\n{Code.ToLowerInvariant()}");

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
