using System.Text.Json;
using WatchLists.ExtensionMethods;
using WatchLists.Logger;
using WatchLists.Services.Enums;
using WatchLists.Utilities;

namespace WatchLists.Services;

public class SettingsService
{
    private readonly string _folder;

    private const string CategoriesFile = "Categories.json";
    private const string StreamingFile  = "StreamingServices.json";
    private const string TypesFile      = "Types.json";

    public SettingsService()
    {
        _folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }

    private async Task<List<string>> LoadOptionsAsync(string fileName)
    {
        var filePath = Path.Combine(_folder
                                  , fileName);
        if (Avails.FileDoesNotExist(filePath))
        {
            var emptyList = new List<string>();
            await SaveOptionsAsync(fileName
                                 , emptyList);

            return emptyList;
        }

        try
        {
            var json    = await File.ReadAllTextAsync(filePath);
            if (json.IsEmpytNullOrWhiteSpace())
            {
                await FileLogger.WriteLogAsync($"The file {filePath} was empty");
                return new List<string>();
            }
            var options = JsonSerializer.Deserialize<List<string>>(json);

            return options ?? new List<string>();
        }
        catch (Exception e)
        {
            await FileLogger.WriteLogAsync($"The file {filePath} was could not be read: {e}");
            return new List<string>();
        }
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
