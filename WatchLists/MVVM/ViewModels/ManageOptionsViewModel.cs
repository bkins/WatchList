using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WatchLists.ExtensionMethods;
using WatchLists.Services;
using WatchLists.Services.Enums;

namespace WatchLists.MVVM.ViewModels;

public partial class ManageOptionsViewModel  : ObservableObject, IQueryAttributable
    {
        private readonly SettingsService  _settingsService;
        private readonly WatchListService _watchListService;

        public SettingType OptionSetting { get; set; }

        public string      OptionKey     { get; set; }

        [ObservableProperty] private string _optionTitle;
        [ObservableProperty] private string _newOption;

        public ObservableCollection<string> Options { get; } = [];


        public ManageOptionsViewModel(SettingsService settingsService, WatchListService watchListService)
        {
            _settingsService  = settingsService;
            _watchListService = watchListService;
        }

        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (! query.TryGetValue("optionKey"
                                  , out object key)) return;

            var keyStr = key.ToString() ?? "";
            if (! Enum.TryParse<SettingType>(keyStr
                                           , out var setting))
            {
                // If parsing fails, default to Categories.
                setting = SettingType.Categories;
            }

            OptionSetting = setting;
            OptionTitle   = $"Manage {OptionSetting}";
            await LoadOptionsAsync();
            //
            // if (! query.TryGetValue("optionKey"
            //                       , out object key)) return;
            //
            // OptionKey   = key?.ToString();
            // OptionTitle = $"Manage {OptionKey}";
            //
            // await LoadOptionsAsync();
        }

        private async Task LoadOptionsAsync()
        {
            var optionsList = await _settingsService.GetOptionsAsync(OptionSetting);
            Options.Clear();
            foreach (string item in optionsList)
            {
                Options.Add(item);
            }
            //
            // string defaultValue = OptionKey switch
            //                       {
            //                               "StreamingServices" => "Netflix,Prime Video,Disney+,Hulu,Max"
            //                             , "Categories"        => "Currently Watching,Finished Watching,Consider Watching"
            //                             , "Types"             => "Show,Movie,Mini-Series"
            //                             , _                   => ""
            //                       };
            //
            // var optionsList = await _settingsService.LoadOptionsAsync($"{OptionKey}.json", defaultValue);
            // Options.Clear();
            // foreach (string item in optionsList)
            // {
            //     Options.Add(item);
            // }
        }

        [RelayCommand]
        private void AddOption()
        {
            if (NewOption.IsEmpytNullOrWhiteSpace()) return;

            Options.Add(NewOption.Trim());

            NewOption = string.Empty;
        }

        [RelayCommand]
        private void DeleteOption(string option)
        {
            Options.Remove(option);
        }

        [RelayCommand]
        private void MoveOptionUp(string option)
        {
            int index = Options.IndexOf(option);

            if (index > 0)
            {
                Options.Move(index, index - 1);
            }
        }

        [RelayCommand]
        private void MoveOptionDown(string option)
        {
            int index = Options.IndexOf(option);

            if (index < Options.Count - 1)
            {
                Options.Move(index, index + 1);
            }
        }

        [RelayCommand]
        private async Task SaveOptions()
        {
            await _settingsService.SaveOptionsAsync(OptionSetting
                                                  , Options.ToList());
            await Shell.Current.GoToAsync("..");
            // await _settingsService.SaveOptionsAsync($"{OptionKey}.json"
            //                                       , Options.ToList());
            // await Shell.Current.GoToAsync("..");
        }
    }
