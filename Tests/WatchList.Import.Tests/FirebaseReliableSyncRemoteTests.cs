using System.Net;
using System.Net.Http.Json;
using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;
using WatchLists.Services.ReliableSync;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class FirebaseReliableSyncRemoteTests
{
    private const string Database = "http://127.0.0.1:9000/?ns=demo-watchlist-default-rtdb";
    private const string Password = "synthetic-watchlist-test";

    [FirebaseEmulatorFact]
    public async Task SyncAsync_FirebaseRulesAndTwoDevices_ProtectAndMergeTheSharedDestination()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var first = new DisposableImportDatabase();
        using var second = new DisposableImportDatabase();
        var profile = await CreateAccount(client);
        var other = await CreateAccount(client);
        var database = profile.Validate(allowEmulator: true);
        var auth = new FirebaseIdentityClient(client);
        var session = await auth.SignInAsync(profile, database, Password, CancellationToken.None);
        var otherSession = await auth.SignInAsync(other, database, Password, CancellationToken.None);
        var refreshed = await auth.RefreshAsync(profile, database, session.RefreshToken, CancellationToken.None);
        Assert.Equal(session.UserId, refreshed.UserId);
        var firstRemote = new FirebaseReliableSyncRemote(client, database, profile, session);
        var secondRemote = new FirebaseReliableSyncRemote(client, database, profile, refreshed);
        var firstSync = new ReliableSyncCoordinator(new ReliableSyncStore(first.DatabasePath, profile.Code), firstRemote);
        var secondSync = new ReliableSyncCoordinator(new ReliableSyncStore(second.DatabasePath, profile.Code), secondRemote);
        var item = new WatchItem
                   {
                       Title    = "Emulator film"
                     , Type     = "Movie"
                     , ApiSource = "TMDB"
                     , MovieId  = 17
                     , Category = "Queue"
                   };
        first.Connection.Insert(item);

        var created = await firstSync.SyncAsync(create: true);
        var joined = await secondSync.SyncAsync();

        Assert.Equal("Sync complete.", created.Message);
        Assert.Equal("Sync complete.", joined.Message);
        Assert.Equal(item.Id, second.Connection.Get<WatchItem>(item.Id).Id);

        var stale = await secondRemote.FetchAsync(CancellationToken.None);
        var changed = first.Connection.Get<WatchItem>(item.Id);
        changed.Category = "Finished";
        changed.IsWatched = true;
        changed.PreviousCategory = "Queue";
        first.Connection.Update(changed);
        await firstSync.SyncAsync();
        Assert.Equal(CloudSyncWriteResult.Conflict,
                     await secondRemote.WriteAsync(SyncEnvelope.Create(SyncSnapshot.Empty), stale.Etag, CancellationToken.None));
        var liked = second.Connection.Get<WatchItem>(item.Id);
        liked.IsLiked = true;
        second.Connection.Update(liked);
        new RecommendationStore(second.Connection).Apply([new(item.Id, "https://example.test/verified", "Verified source")], DateTime.UtcNow);
        await secondSync.SyncAsync();
        await firstSync.SyncAsync();

        var merged = first.Connection.Get<WatchItem>(item.Id);
        Assert.True(merged.IsLiked);
        Assert.True(merged.IsWatched);
        Assert.Equal("Finished", merged.Category);
        Assert.Equal("Queue", merged.PreviousCategory);
        Assert.Equal(item.Id, Assert.Single(RecommendationStore.ReadExisting(first.Connection)).WatchItemId);

        var root = $"http://127.0.0.1:9000/watchlistV3/{session.UserId}/{profile.Code}.json?ns=demo-watchlist-default-rtdb";
        using var anonymous = await client.GetAsync(root);
        using var otherRead = await client.GetAsync($"{root}&auth={Uri.EscapeDataString(otherSession.IdToken)}");
        using var otherWrite = await client.PutAsJsonAsync($"{root}&auth={Uri.EscapeDataString(otherSession.IdToken)}", SyncEnvelope.Create(SyncSnapshot.Empty));
        using var legacy = await client.PutAsJsonAsync($"{root}&auth={Uri.EscapeDataString(session.IdToken)}", new
        {
            schemaVersion = 2,
            revision = Guid.NewGuid().ToString("N"),
            payload = "{}"
        });
        using var delete = await client.DeleteAsync($"{root}&auth={Uri.EscapeDataString(session.IdToken)}");

        AssertDenied(anonymous);
        AssertDenied(otherRead);
        AssertDenied(otherWrite);
        AssertDenied(legacy);
        AssertDenied(delete);
        Assert.Equal(item.Id, Assert.Single((await firstRemote.FetchAsync(CancellationToken.None)).Envelope!.Decode().Items).Id);
    }

    private static async Task<FirebaseSyncProfile> CreateAccount(HttpClient client)
    {
        var profile = new FirebaseSyncProfile(Database, "fake-api-key", $"test-{Guid.NewGuid():N}@example.test", Guid.NewGuid().ToString("N"), false);
        using var response = await client.PostAsJsonAsync("http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:signUp?key=fake-api-key",
                                                          new { email = profile.Email, password = Password, returnSecureToken = true });
        Assert.True(response.IsSuccessStatusCode, $"Demo Auth account setup returned {(int)response.StatusCode}.");
        return profile;
    }

    private static void AssertDenied(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                    $"Expected access denial, received {(int)response.StatusCode}.");
    }
}
