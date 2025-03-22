using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
// For Preferences
using System.Collections.ObjectModel;
using WatchLists.ExtensionMethods;
using WatchLists.Services;

namespace WatchLists.MVVM.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;

    public ObservableCollection<string> StreamingServices { get; } = new();
    public ObservableCollection<string> Categories        { get; } = new();
    public ObservableCollection<string> Types             { get; } = new();

    [ObservableProperty] private string _selectedStreamingService;
    [ObservableProperty] private string _selectedCategory;
    [ObservableProperty] private string _selectedType;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task LoadSettingsAsync()
    {
        var defaultStreaming  = "Netflix,Prime Video,Disney+,Hulu,Max";
        var defaultCategories = "Currently Watching,Finished Watching,Consider Watching";
        var defaultTypes      = "Show,Movie,Mini-Series";

        var streaming = await _settingsService.LoadOptionsAsync("StreamingServices.json"
                                                              , defaultStreaming);
        var categories = await _settingsService.LoadOptionsAsync("Categories.json"
                                                               , defaultCategories);
        var types = await _settingsService.LoadOptionsAsync("Types.json"
                                                          , defaultTypes);

        StreamingServices.Clear();
        foreach (var item in streaming)
        {
            StreamingServices.Add(item);
        }

        Categories.Clear();
        foreach (var item in categories)
        {
            Categories.Add(item);
        }

        Types.Clear();
        foreach (var item in types)
        {
            Types.Add(item);
        }

        if (StreamingServices.Count > 0
         && SelectedStreamingService.IsEmpytNullOrWhiteSpace())
        {
            SelectedStreamingService = StreamingServices[0];
        }

        if (Categories.Count > 0
         && SelectedCategory.IsEmpytNullOrWhiteSpace())
        {
            SelectedCategory = Categories[0];
        }

        if (Types.Count > 0
         && SelectedType.IsEmpytNullOrWhiteSpace())
        {
            SelectedType = Types[0];
        }
    }

    [RelayCommand]
    public async Task NavigateToManageOptions(string optionKey)
    {
        await Shell.Current.GoToAsync($"ManageOptionsPage?optionKey={optionKey}");
    }
}
