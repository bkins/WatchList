using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
// For Preferences
using System.Collections.ObjectModel;
using System.Text.Json;
using WatchLists.ExtensionMethods;
using WatchLists.MVVM.Models;
using WatchLists.Services;
using WatchLists.Services.Enums;

namespace WatchLists.MVVM.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;

    [ObservableProperty] private string _selectedStreamingService;
    [ObservableProperty] private string _selectedCategory;
    [ObservableProperty] private string _selectedType;

    public ObservableCollection<string> StreamingServices { get; } = new();
    public ObservableCollection<string> Categories        { get; } = new();
    public ObservableCollection<string> Types             { get; } = new();

    public  string? WatchedCategory { get; set; }
    private string  _selectedWatchedCategory;

    private readonly string _settingsFilePath = Path.Combine(FileSystem.AppDataDirectory
                                                           , "settings.json");

    public string SelectedWatchedCategory
    {
        get => _selectedWatchedCategory;
        set
        {
            if (_selectedWatchedCategory == value) return;

            _selectedWatchedCategory = value;
            OnPropertyChanged();
        }
    }

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        WatchedCategory  = _settingsService.WatchedCategory.ToString();
    }

    public async Task LoadSettingsAsync()
    {
        var streaming = await _settingsService.GetOptionsAsync(SettingType.StreamingServices);
        var categories = await _settingsService.GetOptionsAsync(SettingType.Categories);
        var types = await _settingsService.GetOptionsAsync(SettingType.Types);

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

        // if (StreamingServices.Count > 0
        //  && SelectedStreamingService.IsEmptyNullOrWhiteSpace())
        // {
        //     SelectedStreamingService = StreamingServices[0];
        // }
        //
        // if (Categories.Count > 0
        //  && SelectedCategory.IsEmptyNullOrWhiteSpace())
        // {
        //     SelectedCategory = Categories[0];
        // }
        //
        // if (Types.Count > 0
        //  && SelectedType.IsEmptyNullOrWhiteSpace())
        // {
        //     SelectedType = Types[0];
        // }
    }



    [RelayCommand]
    public async Task NavigateToManageOptions(string optionKey)
    {
        await Shell.Current.GoToAsync($"ManageOptionsPage?optionKey={optionKey}");
    }
}
