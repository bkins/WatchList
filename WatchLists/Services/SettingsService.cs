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

    public SettingsService()
    {
        _folder = FileSystem.AppDataDirectory;
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
}
