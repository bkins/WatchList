using System.Net.Http.Json;
using System.Text.Json;

namespace WatchLists.Services.ReliableSync;

public sealed class FirebaseIdentityClient
{
    private readonly HttpClient _httpClient;

    public FirebaseIdentityClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<FirebaseSession> SignInAsync(FirebaseSyncProfile profile
                                            , Uri                 database
                                            , string              password
                                            , CancellationToken   cancellationToken)
    {
        var body = JsonContent.Create(new { email = profile.Email, password, returnSecureToken = true });
        return PostAsync(Endpoint(database, "identitytoolkit.googleapis.com", "v1/accounts:signInWithPassword", profile.ApiKey)
                       , body, "localId", "idToken", "refreshToken", cancellationToken);
    }

    public Task<FirebaseSession> RefreshAsync(FirebaseSyncProfile profile
                                             , Uri                 database
                                             , string              token
                                             , CancellationToken   cancellationToken)
    {
        var body = new FormUrlEncodedContent(new Dictionary<string, string>
                                             {
                                                 ["grant_type"]    = "refresh_token"
                                               , ["refresh_token"] = token
                                             });
        return PostAsync(Endpoint(database, "securetoken.googleapis.com", "v1/token", profile.ApiKey)
                       , body, "user_id", "id_token", "refresh_token", cancellationToken);
    }

    private async Task<FirebaseSession> PostAsync(Uri               endpoint
                                                 , HttpContent       body
                                                 , string            userIdField
                                                 , string            idTokenField
                                                 , string            refreshTokenField
                                                 , CancellationToken cancellationToken)
    {
        using (body)
        using (var response = await _httpClient.PostAsync(endpoint, body, cancellationToken))
        {
            if (!response.IsSuccessStatusCode)
                throw new InvalidDataException("Firebase sign-in failed. Check the account, project and credentials.");

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            return new FirebaseSession(Read(root, userIdField), Read(root, idTokenField), Read(root, refreshTokenField));
        }
    }

    private static string Read(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
            throw new InvalidDataException("Firebase authentication returned an incomplete session.");
        return property.GetString()!;
    }

    private static Uri Endpoint(Uri database, string service, string path, string key)
    {
        var address = database.Scheme == "http"
            ? $"http://{database.Host}:9099/{service}/{path}"
            : $"https://{service}/{path}";
        return new Uri($"{address}?key={Uri.EscapeDataString(key)}");
    }
}
