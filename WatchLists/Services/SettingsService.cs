using System.Text.Json;
using WatchLists.ExtensionMethods;
using WatchLists.Logger;
using WatchLists.Services.Enums;
using WatchLists.Utilities;

namespace WatchLists.Services;

public class SettingsService
{
    private readonly string _folder;

    private const string CategoriesFile      = "Categories.json";
    private const string StreamingFile       = "StreamingServices.json";
    private const string TypesFile           = "Types.json";
    private const string WatchedCategoryFile = "WatchedCategory.json";
    private const string SyncSettingsFile    = "SyncSettings.json";

    public SettingsService (string? storageFolder = null)
    {
        if (storageFolder.HasValue())
        {
            _folder = storageFolder;
        }
        else
        {
            try
            {
                _folder = FileSystem.AppDataDirectory;
            }
            catch
            {
                _folder = Path.Combine(Path.GetTempPath(), "WatchList");
                Directory.CreateDirectory(_folder);
            }
        }
    }

    public async Task<string> GetWatchedCategoryAsync()
    {
        var filePath = Path.Combine(_folder
                                  , WatchedCategoryFile);
        if (Avails.FileDoesNotExist(filePath))
        {
            var defaultWatched = "Finished Watching";
            await SaveWatchedCategoryAsync(defaultWatched);

            await FileLogger.WriteLogAsync($"Watched Category file {WatchedCategoryFile} was not found. It was created with default: {defaultWatched}.");

            return defaultWatched;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var val  = JsonSerializer.Deserialize<string>(json);
            return string.IsNullOrWhiteSpace(val) ? "Finished Watching" : val;
        }
        catch
        {
            return "Finished Watching";
        }
    }

    public async Task SaveWatchedCategoryAsync (string watchedCategory)
    {
        var filePath = Path.Combine(_folder
                                  , WatchedCategoryFile);
        var json = JsonSerializer.Serialize(watchedCategory
                                          , new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath
                                   , json);
    }

    private async Task<List<string>> LoadOptionsAsync(string fileName)
    {
        var filePath = Path.Combine(_folder
                                  , fileName);
        if (Avails.FileDoesNotExist(filePath))
        {
            var defaultOptions = GetDefaultOptions(fileName);
            await SaveOptionsAsync(fileName
                                 , defaultOptions);

            return defaultOptions;
        }

        try
        {
            var json    = await File.ReadAllTextAsync(filePath);
            if (json.IsEmptyNullOrWhiteSpace())
            {
                await FileLogger.WriteLogAsync($"The file {filePath} was empty. Initializing defaults.");
                var defaultOptions = GetDefaultOptions(fileName);
                await SaveOptionsAsync(fileName
                                     , defaultOptions);
                return defaultOptions;
            }
            var options = JsonSerializer.Deserialize<List<string>>(json);
            if (options == null || options.Count == 0)
            {
                await FileLogger.WriteLogAsync($"The file {filePath} was empty or contained no elements. Initializing defaults.");
                var defaultOptions = GetDefaultOptions(fileName);
                await SaveOptionsAsync(fileName
                                     , defaultOptions);
                return defaultOptions;
            }

            return options;
        }
        catch (Exception e)
        {
            await FileLogger.WriteLogAsync($"The file {filePath} could not be read: {e}. Returning defaults.");
            return GetDefaultOptions(fileName);
        }
    }

    private List<string> GetDefaultOptions(string fileName)
    {
        return fileName switch
        {
            CategoriesFile => new List<string>
                              {
                                  "Currently Watching"
                                , "Finished Watching"
                                , "Consider Watching"
                              },
            StreamingFile  => new List<string>
                              {
                                  "Netflix"
                                , "Prime Video"
                                , "Disney+"
                                , "Hulu"
                                , "Max"
                              },
            TypesFile      => new List<string>
                              {
                                  "Show"
                                , "Movie"
                                , "Mini-Series"
                              },
            _              => new List<string>()
        };
    }

    // public List<string> GetCategories()
    // {
    //     var filePath = Path.Combine(_folder
    //                               , CategoriesFile);
    //
    //     if ( Avails.FileDoesNotExist(filePath)) return [];
    //
    //     try
    //     {
    //         var json = File.ReadAllText(filePath);
    //
    //         return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    //     }
    //     catch
    //     {
    //         return [];
    //     }
    // }
    //
    // public async Task SaveCategoriesAsync(List<string> categories)
    // {
    //     string filePath = Path.Combine(_folder
    //                                  , CategoriesFile);
    //     string json = JsonSerializer.Serialize(categories
    //                                          , new JsonSerializerOptions { WriteIndented = true });
    //     await File.WriteAllTextAsync(filePath
    //                                , json);
    // }
    //
    // public async Task AddCategoryAsync(string category)
    // {
    //     var categories = GetCategories();
    //     if (categories.DoesNotContain(category))
    //     {
    //         categories.Add(category);
    //         await SaveCategoriesAsync(categories);
    //     }
    // }
    private async Task SaveOptionsAsync(string       fileName
                                     , List<string> options)
    {
        var filePath = Path.Combine(_folder
                                  , fileName);
        var json = JsonSerializer.Serialize(options
                                          , new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath
                                   , json);
    }

    public async Task SaveOptionsAsync (SettingType  setting
                                      , List<string> options)
    {
        string fileName = GetSettingTypeFileName(setting);

        await SaveOptionsAsync(fileName
                             , options);
    }
    public async Task<List<string>> GetOptionsAsync (SettingType setting)
    {
        string fileName = GetSettingTypeFileName(setting);

        return await LoadOptionsAsync(fileName);
    }

    private static string GetSettingTypeFileName (SettingType setting)
    {
        string fileName = setting switch
                          {
                                  SettingType.Categories        => CategoriesFile
                                , SettingType.StreamingServices => StreamingFile
                                , SettingType.Types             => TypesFile

                                , _ => throw new ArgumentOutOfRangeException(nameof(setting)
                                                                           , "Unsupported setting type")
                          };

        return fileName;
    }

    public async Task AddOptionAsync (SettingType setting
                                    , string      option)
    {
        var options = await GetOptionsAsync(setting);
        if (options.DoesNotContain(option))
        {
            options.Add(option);
            await SaveOptionsAsync(setting
                                 , options);
        }
    }

    private SyncSettingsConfig LoadSyncSettingsConfig()
    {
        var filePath = Path.Combine(_folder, SyncSettingsFile);
        if (Avails.FileDoesNotExist(filePath)) return new SyncSettingsConfig();

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<SyncSettingsConfig>(json) ?? new SyncSettingsConfig();
        }
        catch
        {
            return new SyncSettingsConfig();
        }
    }

    private async Task SaveSyncSettingsConfigAsync (SyncSettingsConfig config)
    {
        var filePath = Path.Combine(_folder, SyncSettingsFile);
        var json     = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    public string GetSyncFolderPath()
    {
        return LoadSyncSettingsConfig().SyncFolderPath;
    }

    public async Task SaveSyncFolderPathAsync (string path)
    {
        var config            = LoadSyncSettingsConfig();
        config.SyncFolderPath = path ?? string.Empty;
        await SaveSyncSettingsConfigAsync(config);
    }

    public bool GetAutoSyncEnabled()
    {
        return LoadSyncSettingsConfig().AutoSyncEnabled;
    }

    public async Task SaveAutoSyncEnabledAsync (bool enabled)
    {
        var config             = LoadSyncSettingsConfig();
        config.AutoSyncEnabled = enabled;
        await SaveSyncSettingsConfigAsync(config);
    }

    public string GetSyncMode()
    {
        return LoadSyncSettingsConfig().SyncMode;
    }

    public async Task SaveSyncModeAsync (string mode)
    {
        var config      = LoadSyncSettingsConfig();
        config.SyncMode = mode ?? "CloudApi";
        await SaveSyncSettingsConfigAsync(config);
    }

    public string GetApiEndpointUrl()
    {
        var url = LoadSyncSettingsConfig().ApiEndpointUrl;
        if (url.IsEmptyNullOrWhiteSpace() || 
            url.EqualsIgnoreCase("https://watchlist-app-sync-default-rtdb.firebaseio.com") || 
            url.EqualsIgnoreCase("https://watchlist-app-sync-default-rtdb.firebaseio.com/"))
        {
            return "https://watchlist-faa16-default-rtdb.firebaseio.com/";
        }
        return url;
    }

    public async Task SaveApiEndpointUrlAsync (string url)
    {
        var config            = LoadSyncSettingsConfig();
        config.ApiEndpointUrl = url ?? string.Empty;
        await SaveSyncSettingsConfigAsync(config);
    }

    public string GetSyncCode()
    {
        return LoadSyncSettingsConfig().SyncCode;
    }

    public async Task SaveSyncCodeAsync (string code)
    {
        var config      = LoadSyncSettingsConfig();
        config.SyncCode = code ?? string.Empty;
        await SaveSyncSettingsConfigAsync(config);
    }
}

public class SyncSettingsConfig
{
    public string SyncMode        { get; set; } = "CloudApi";
    public string SyncFolderPath  { get; set; } = string.Empty;
    public string ApiEndpointUrl  { get; set; } = "https://watchlist-faa16-default-rtdb.firebaseio.com/";
    public string SyncCode        { get; set; } = "MyWatchList2026";
    public bool   AutoSyncEnabled { get; set; } = true;
}
