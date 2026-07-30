using System.Net.Http.Json;
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

    public async Task<string?> CreateNewCloudSyncBlobAsync()
    {
        try
        {
            var content  = new StringContent("{\"items\":[]}", System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://jsonblob.com/api/jsonBlob", content);

            if (response.IsSuccessStatusCode && response.Headers.Location != null)
            {
                var location = response.Headers.Location.ToString();
                if (! location.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    location = "https://jsonblob.com" + location;
                }

                await FileLogger.WriteLogAsync($"CreateNewCloudSyncBlobAsync: Created cloud endpoint {location}");
                return location;
            }

            await FileLogger.WriteLogAsync($"CreateNewCloudSyncBlobAsync HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            return null;
        }
        catch (Exception ex)
        {
            await FileLogger.WriteLogAsync($"CreateNewCloudSyncBlobAsync error: {ex.Message}");
            return null;
        }
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

    public async Task<bool> UploadBundleAsync (string endpointUrl, string syncCode, SyncBundle bundle)
    {
        var requestUrl = BuildRequestUrl(endpointUrl, syncCode);
        if (requestUrl.IsEmptyNullOrWhiteSpace())
        {
            await FileLogger.WriteLogAsync("UploadBundleAsync: Unable to construct valid endpoint URL.");
            return false;
        }

        try
        {
            var json    = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = false });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(requestUrl, content);

            if (! response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                await FileLogger.WriteLogAsync($"UploadBundleAsync HTTP {(int)response.StatusCode}: {response.ReasonPhrase} - {errorBody}");
                return false;
            }

            await FileLogger.WriteLogAsync($"UploadBundleAsync: Successfully uploaded {bundle.Items.Count} items to cloud ({requestUrl}).");
            return true;
        }
        catch (Exception ex)
        {
            await FileLogger.WriteLogAsync($"UploadBundleAsync exception: {ex.Message}");
            return false;
        }
    }

    public static string BuildRequestUrl (string endpointUrl, string syncCode)
    {
        var cleanUrl  = endpointUrl.Trim().TrimEnd('/');
        var cleanCode = syncCode.Trim();

        // 1. Full URL provided
        if (cleanUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            if (cleanUrl.Contains("/jsonBlob/", StringComparison.OrdinalIgnoreCase) || cleanUrl.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return cleanUrl;
            }

            if (cleanUrl.Contains("firebaseio.com", StringComparison.OrdinalIgnoreCase) ||
                cleanUrl.Contains("firebasedatabase.app", StringComparison.OrdinalIgnoreCase))
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
