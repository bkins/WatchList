using System.Net;
using System.Text;
using WatchLists.Services.MuseImport;

namespace WatchLists.Services.ReliableSync;

public sealed class FirebaseReliableSyncRemote : IReliableSyncRemote
{
    private readonly HttpClient _httpClient;
    private readonly Uri        _address;
    private readonly Uri        _anonymousAddress;
    private bool _accessChecked;

    public FirebaseReliableSyncRemote(HttpClient httpClient, Uri database, FirebaseSyncProfile profile, FirebaseSession session)
    {
        if (session.UserId.Length is 0 or > 128 || session.UserId.Any(character => !char.IsAsciiLetterOrDigit(character)
                                                                               && character is not '-' and not '_'))
            throw new InvalidDataException("Firebase returned an unsupported account identifier.");
        _httpClient = httpClient;
        var builder = new UriBuilder(database)
                      {
                          Path = $"/watchlistV3/{session.UserId}/{profile.Code.ToLowerInvariant()}.json"
                      };
        _anonymousAddress = builder.Uri;
        var query = builder.Query.TrimStart('?');
        builder.Query = $"{(query.Length > 0 ? $"{query}&" : string.Empty)}auth={Uri.EscapeDataString(session.IdToken)}";
        _address = builder.Uri;
    }

    public async Task<SyncRemoteRead> FetchAsync(CancellationToken cancellationToken)
    {
        if (!_accessChecked)
        {
            using var check = await _httpClient.GetAsync(_anonymousAddress, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (check.StatusCode is not HttpStatusCode.Unauthorized and not HttpStatusCode.Forbidden)
                throw new InvalidDataException("Firebase did not deny unauthenticated reads. Verify the private database rules before syncing.");
            _accessChecked = true;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, _address);
        request.Headers.Add("X-Firebase-ETag", "true");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode || !response.Headers.TryGetValues("ETag", out var values))
            throw new InvalidDataException("Firebase could not read a revision. Check pairing, credentials and access rules.");

        var etag = values.SingleOrDefault();
        if (string.IsNullOrWhiteSpace(etag))
            throw new InvalidDataException("Firebase omitted the revision token.");

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return content.Trim() == "null"
            ? new SyncRemoteRead(null, "null_etag")
            : new SyncRemoteRead(SyncJson.Read<SyncEnvelope>(content), etag);
    }

    public async Task<CloudSyncWriteResult> WriteAsync(SyncEnvelope envelope, string etag, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(etag) || etag == "*")
            throw new InvalidDataException("A specific Firebase revision is required to write.");

        using var request = new HttpRequestMessage(HttpMethod.Put, _address);
        if (!request.Headers.TryAddWithoutValidation("if-match", etag))
            throw new InvalidDataException("Firebase returned an invalid revision token.");
        request.Content = new StringContent(SyncJson.Write(envelope), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.PreconditionFailed) return CloudSyncWriteResult.Conflict;
        if (!response.IsSuccessStatusCode) return CloudSyncWriteResult.Failed;

        // A conditional PUT normally echoes the stored revision. A malformed acknowledgement
        // stays pending until a fresh read proves that the exact prepared revision exists.
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return SyncJson.Read<SyncEnvelope>(content) == envelope ? CloudSyncWriteResult.Success : CloudSyncWriteResult.Failed;
    }
}
