using System.Text.Json;
using WatchLists.ExtensionMethods;
using WatchLists.Utilities;

namespace WatchLists.Services;

public class SettingsService
{
    private readonly string _folder;

    private const    string CategoriesFile = "Categories.json";

    public SettingsService()
    {
        _folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }

    public async Task<List<string>> LoadOptionsAsync(string fileName
                                                   , string defaultValues)
    {
        var filePath = Path.Combine(_folder
                                  , fileName);
        if (! File.Exists(filePath))
        {
            // If the file doesn't exist, write the default values.
            var defaults = defaultValues.Split(',').Select(option => option.Trim()).ToList();
            await SaveOptionsAsync(fileName
                                 , defaults);

            return defaults;
        }

        try
        {
            var json    = await File.ReadAllTextAsync(filePath);
            var options = JsonSerializer.Deserialize<List<string>>(json);

            return options ?? defaultValues.Split(',').Select(option => option.Trim()).ToList();
        }
        catch
        {
            // In case of error, return defaults.
            return defaultValues.Split(',').Select(s => s.Trim()).ToList();
        }
    }

    public List<string> GetCategories()
    {
        var filePath = Path.Combine(_folder
                                  , CategoriesFile);

        if ( Avails.FileDoesNotExist(filePath)) return [];

        try
        {
            var json = File.ReadAllText(filePath);

            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return [];
        }
    }

    public async Task SaveCategoriesAsync(List<string> categories)
    {
        string filePath = Path.Combine(_folder
                                     , CategoriesFile);
        string json = JsonSerializer.Serialize(categories
                                             , new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath
                                   , json);
    }

    public async Task AddCategoryAsync(string category)
    {
        var categories = GetCategories();
        if (categories.DoesNotContain(category))
        {
            categories.Add(category);
            await SaveCategoriesAsync(categories);
        }
    }
    public async Task SaveOptionsAsync(string       fileName
                                     , List<string> options)
    {
        var filePath = Path.Combine(_folder
                                  , fileName);
        var json = JsonSerializer.Serialize(options
                                          , new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath
                                   , json);
    }
}
