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

    private string _selectedWatchedCategory;

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

        SelectedWatchedCategory = await _settingsService.GetWatchedCategoryAsync();
    }

    [RelayCommand]
    public async Task SaveSettings()
    {
        await _settingsService.SaveWatchedCategoryAsync(SelectedWatchedCategory);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    public async Task NavigateToManageOptions(string optionKey)
    {
        await Shell.Current.GoToAsync($"ManageOptionsPage?optionKey={optionKey}");
    }
}
