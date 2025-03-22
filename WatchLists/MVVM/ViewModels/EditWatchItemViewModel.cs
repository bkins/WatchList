using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchLists.ExtensionMethods;
using WatchLists.Logger;
using WatchLists.Messaging;
using WatchLists.MVVM.Models;
using WatchLists.MVVM.Views;
using WatchLists.Services;

namespace WatchLists.MVVM.ViewModels;

public partial class EditWatchItemViewModel : ObservableObject
{
    [ObservableProperty] private bool      _isWatched;
    [ObservableProperty] private bool      _isLiked;
    [ObservableProperty] private bool      _isDisliked;
    [ObservableProperty] private string    _selectedType = string.Empty;
    [ObservableProperty] private WatchItem _editableItem = new();

    private readonly WatchListService _watchListService;
    private readonly SettingsService  _settingsService;

    public WatchItem                    OriginalItem     { get; set; } = new();
    public ObservableCollection<string> Categories       { get; set; }
    public string                       SelectedCategory { get; set; } = string.Empty;

    public ObservableCollection<string> StreamingServices { get; set; }

    public List<string> AvailableTypes { get; } = new()
                                                  {
                                                          "Show"
                                                        , "Movie"
                                                        , "Mini-Series"
                                                  };

    public EditWatchItemViewModel (WatchListService watchListService
                                 , SettingsService  settingsService)
    {
        _settingsService  = settingsService;
        _watchListService = watchListService;

        Categories = new ObservableCollection<string>(_settingsService.GetCategories());

        StreamingServices = new ObservableCollection<string>
                            {
                                    "Netflix"
                                  , "Prime Video"
                                  , "Disney+"
                                  , "Hulu"
                                  , "Max"
                            };

        // Subscribe to movie selection messages
        MessagingCenter.Subscribe<SearchPage, MovieSelectedMessage>(this
                                                                  , "MovieSelected"
                                                                  , (_
                                                                   , message) =>
                                                                    {
                                                                        var movie = message.SelectedMovie;
                                                                        EditableItem.Title = movie.Title;
                                                                        EditableItem.DeepLinkUri =
                                                                                $"https://www.themoviedb.org/movie/{movie.Id}";
                                                                    });
    }

    [RelayCommand]
    public async Task Save()
    {
        FileLogger.WriteLogAsync("Save command invoked.");
        //Debug.WriteLine("Save command invoked.");

        OriginalItem.Title            = EditableItem.Title;
        OriginalItem.StreamingService = EditableItem.StreamingService;
        OriginalItem.Category         = EditableItem.Category;
        OriginalItem.DeepLinkUri      = EditableItem.DeepLinkUri;
        OriginalItem.LastUpdated      = DateTime.Now;
        OriginalItem.IsWatched        = IsWatched;
        OriginalItem.IsLiked          = EditableItem.IsLiked;
        OriginalItem.Type             = EditableItem.Type;

        _watchListService.UpdateWatchItem(OriginalItem);
        _ = FileLogger.WriteLogAsync("Save command: UpdateWatchItem executed.");
        // Debug.WriteLine("Save command: UpdateWatchItem executed.");

        await Shell.Current.GoToAsync("..");
        _ = FileLogger.WriteLogAsync("Save command: Navigation complete.");
        //Debug.WriteLine("Save command: Navigation complete.");
    }

    [RelayCommand]
    private void ToggleIsLiked (bool like)
    {
        IsLiked              = like;
        IsDisliked           = ! like;
        EditableItem.IsLiked = like;
    }

    [RelayCommand]
    public async Task OpenSearch()
    {
        await Shell.Current.GoToAsync(nameof(SearchPage));
    }

    public async Task OnCategorySelectedAsync (string category)
    {
        if (category.HasValue()
         && Categories.NotContains(category))
        {
            Categories.Add(category);
            await _settingsService.AddCategoryAsync(category);
        }

        SelectedCategory = category;
    }

    public void LoadItem (string? watchItemId)
    {
        if (watchItemId.IsEmpytNullOrWhiteSpace())
        {
            // New item mode: initialize a new WatchItem.
            EditableItem = new WatchItem
                           {
                                   Category         = Categories.FirstOrDefault() ?? ""
                                 , StreamingService = StreamingServices.FirstOrDefault() ?? ""
                                 , Type             = AvailableTypes.FirstOrDefault() ?? ""
                           };

            OriginalItem = EditableItem;

            return;
        }

        var item = _watchListService.GetWatchItems()
                                    .FirstOrDefault(watchItem => watchItem.Id
                                                                          .ToString()
                                                              == watchItemId);

        if (item == null) return;

        OriginalItem = item;
        EditableItem = new WatchItem
                       {
                               Id               = item.Id
                             , Title            = item.Title
                             , StreamingService = item.StreamingService
                             , Category         = item.Category
                             , DeepLinkUri      = item.DeepLinkUri
                             , LastUpdated      = item.LastUpdated
                             , IsWatched        = item.IsWatched
                             , IsLiked          = item.IsLiked
                             , Type             = item.Type
                       };

        // Sync VM properties
        IsWatched        = item.IsWatched;
        IsLiked          = item.IsLiked;
        SelectedType     = item.Type;
        SelectedCategory = item.Category;
    }
}
