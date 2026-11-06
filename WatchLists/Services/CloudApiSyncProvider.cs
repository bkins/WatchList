using System.Net;
using WatchLists.Services.MuseImport;
using System.Text.Json;
using WatchLists.ExtensionMethods;
using WatchLists.Logger;
using WatchLists.Services.Interfaces;
using WatchLists.Services.Models;

namespace WatchLists.Services;

public class CloudApiSyncProvider : IRemoteSyncProvider
{
    private readonly HttpClient _httpClient;

    public CloudApiSyncProvider (HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<SyncBundle?> FetchLatestBundleAsync (string endpointUrl, string syncCode)
    {
        var requestUrl = BuildRequestUrl(endpointUrl, syncCode);
        if (requestUrl.IsEmptyNullOrWhiteSpace())
        {
            await FileLogger.WriteLogAsync("FetchLatestBundleAsync: Unable to construct valid endpoint URL.");
            return null;
        }

        try
        {
            var response = await _httpClient.GetAsync(requestUrl);

            if (! response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                await FileLogger.WriteLogAsync($"FetchLatestBundleAsync HTTP {(int)response.StatusCode}: {response.ReasonPhrase} - {errorBody}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            if (json.IsEmptyNullOrWhiteSpace() || json == "null")
            {
                return null;
            }

            return JsonSerializer.Deserialize<SyncBundle>(json);
        }
        catch (Exception ex)
        {
            await FileLogger.WriteLogAsync($"FetchLatestBundleAsync exception: {ex.Message}");
            return null;
        }
    }

    public async Task<CloudSyncReadResult?> FetchConditionalAsync (string endpointUrl, string syncCode)
    {
        var requestUrl = BuildFirebaseRequestUrl(endpointUrl, syncCode);
        if (requestUrl == null) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.TryAddWithoutValidation("X-Firebase-ETag", "true");
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode || !response.Headers.TryGetValues("ETag", out var values)) return null;

            var etag = values.SingleOrDefault();
            if (string.IsNullOrWhiteSpace(etag)) return null;
            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json)) return null;
            var bundle = json.Trim() == "null" ? null : JsonSerializer.Deserialize<SyncBundle>(json);
            return new CloudSyncReadResult(bundle, etag);
        }
        catch (Exception exception)
        {
            await FileLogger.WriteLogAsync($"Conditional cloud read failed: {exception.GetType().Name}");
            return null;
        }
    }

    public async Task<CloudSyncWriteResult> UploadConditionalAsync (string endpointUrl
                                                                  , string syncCode
                                                                  , SyncBundle bundle
                                                                  , string etag)
    {
        var requestUrl = BuildFirebaseRequestUrl(endpointUrl, syncCode);
        if (requestUrl == null || string.IsNullOrWhiteSpace(etag)) return CloudSyncWriteResult.Failed;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, requestUrl);
            request.Headers.TryAddWithoutValidation("if-match", etag);
            request.Content = new StringContent(JsonSerializer.Serialize(bundle), System.Text.Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.PreconditionFailed) return CloudSyncWriteResult.Conflict;
            return response.IsSuccessStatusCode ? CloudSyncWriteResult.Success : CloudSyncWriteResult.Failed;
        }
        catch (Exception exception)
        {
            await FileLogger.WriteLogAsync($"Conditional cloud upload failed: {exception.GetType().Name}");
            return CloudSyncWriteResult.Failed;
        }
    }

    private static string? BuildFirebaseRequestUrl (string endpointUrl, string syncCode)
    {
        if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
            return null;
        var host = endpoint.Host;
        if (!host.EndsWith(".firebaseio.com", StringComparison.OrdinalIgnoreCase)
            && !host.EndsWith(".firebasedatabase.app", StringComparison.OrdinalIgnoreCase)) return null;
        if (!string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment)) return null;
        return BuildRequestUrl(endpointUrl, syncCode);
    }
    public static string BuildRequestUrl (string endpointUrl, string syncCode)
    {
        var cleanUrl  = endpointUrl.Trim().TrimEnd('/');
        var cleanCode = syncCode.Trim();

        // 1. Full URL provided
        if (cleanUrl.StartsWithIgnoreCase("http"))
        {
            if (cleanUrl.ContainsIgnoreCase("/jsonBlob/") || cleanUrl.EndsWithIgnoreCase(".json"))
            {
                return cleanUrl;
            }

            if (cleanUrl.ContainsIgnoreCase("firebaseio.com") ||
                cleanUrl.ContainsIgnoreCase("firebasedatabase.app"))
            {
                return $"{cleanUrl}/watchlist/{Uri.EscapeDataString(cleanCode)}.json";
            }

            if (cleanCode.HasValue())
            {
                return $"{cleanUrl}/{Uri.EscapeDataString(cleanCode)}";
            }

            return cleanUrl;
        }

        // 2. Short ID or code provided -> use jsonblob.com
        if (cleanCode.HasValue())
        {
            return $"https://jsonblob.com/api/jsonBlob/{cleanCode}";
        }

        return string.Empty;
    }
}
