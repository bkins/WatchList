using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WatchLists.ExtensionMethods;
using WatchLists.Logger;
using WatchLists.Messaging;
using WatchLists.MVVM.Models;
using WatchLists.MVVM.Views;
using WatchLists.Services;
using WatchLists.Services.Enums;

namespace WatchLists.MVVM.ViewModels;

public partial class EditWatchItemViewModel : ObservableObject
{
    [ObservableProperty] private bool      _isWatched;
    [ObservableProperty] private bool      _isLiked;
    [ObservableProperty] private bool      _isDisliked;
    [ObservableProperty] private string    _selectedType = string.Empty;
    [ObservableProperty] private WatchItem _editableItem = new();

    private string _previousCategory = string.Empty;
    private bool   _isLoaded         = false;

    private readonly WatchListService _watchListService;
    private readonly SettingsService  _settingsService;

    public WatchItem                    OriginalItem     { get; set; } = new();
    public ObservableCollection<string> Categories       { get; set; }
    public string                       SelectedCategory { get; set; } = string.Empty;

    public string SelectedStreamingService { get; set; } = string.Empty;

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

        Categories        = new ObservableCollection<string>();
        StreamingServices = new ObservableCollection<string>();

        // Register for movie selection messages using modern WeakReferenceMessenger
        WeakReferenceMessenger.Default.Register<MovieSelectedMessage>(this, (recipient, message) =>
        {
            var movie = message.SelectedMovie;
            _ = FileLogger.WriteLogAsync($"[Subscription] MovieSelectedMessage received! Title: '{movie?.Title}', Id: {movie?.Id}");
            var item = new WatchItem
            {
                Id               = EditableItem.Id,
                Title            = movie.Title,
                StreamingService = EditableItem.StreamingService,
                Category         = EditableItem.Category,
                IsWatched        = EditableItem.IsWatched,
                IsLiked          = EditableItem.IsLiked,
                DeepLinkUri      = $"https://www.themoviedb.org/movie/{movie.Id}",
                Type             = "Movie",
                PreviousCategory = EditableItem.PreviousCategory
            };
            EditableItem = item;
            SelectedType = "Movie";
            _ = FileLogger.WriteLogAsync($"[Subscription] Form fields updated: Title='{EditableItem.Title}', Type='{SelectedType}'");
        });
    }

    public async Task InitializeAsync()
    {
        if (Categories.Count > 0 || StreamingServices.Count > 0) return;

        // Load settings asynchronously.
        var categoriesList = await _settingsService.GetOptionsAsync(SettingType.Categories);
        Categories.Clear();
        foreach (var item in categoriesList)
        {
            Categories.Add(item);
        }

        var streamingList = await _settingsService.GetOptionsAsync(SettingType.StreamingServices);
        StreamingServices.Clear();
        foreach (var item in streamingList)
        {
            StreamingServices.Add(item);
        }
    }

    [RelayCommand]
    public async Task ToggleWatched (bool isNowWatched)
    {
        // Get the watched category from settings
        var watchedCategory = await _settingsService.GetWatchedCategoryAsync();

        if (isNowWatched)
        {
            SetCategoryToWatchedAndBackup(watchedCategory);
        }
        else
        {
            RevertToPreviousCategory();
        }

        // Update the isWatched property
        IsWatched = isNowWatched;
    }

    private void RevertToPreviousCategory()
    {
        // If unmarking as watched and we have a previous category, revert:
        if (EditableItem.PreviousCategory.IsEmptyNullOrWhiteSpace()) return;

        EditableItem.Category = _previousCategory;
        _previousCategory     = string.Empty;

        OnPropertyChanged(nameof(EditableItem.Category));
    }

    private void SetCategoryToWatchedAndBackup (string watchedCategory)
    {

        // If marking as watched, and if the current category isn't the watched one:
        if (EditableItem.Category
                        .IsEqualTo(watchedCategory
                                 , StringComparison.OrdinalIgnoreCase)) return;

        EditableItem.PreviousCategory = EditableItem.Category; // backup the current category
        EditableItem.Category         = watchedCategory;

        OnPropertyChanged(nameof(EditableItem.Category));
    }

    [RelayCommand]
    public async Task Save()
    {
        await FileLogger.WriteLogAsync("Save command invoked.");
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

        await Shell.Current.GoToAsync("..");
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

    public async Task OnStreamingServiceSelectedAsync (string streamingService)
    {
        if (streamingService.HasValue()
            && StreamingServices.DoesNotContains(streamingService))
        {
            StreamingServices.Add(streamingService);
            await _settingsService.AddOptionAsync(SettingType.StreamingServices
                                                , streamingService);
        }
        SelectedStreamingService = streamingService;
    }

    public async Task OnCategorySelectedAsync (string category)
    {
        // If the category doesn't exist yet, add it and persist in settings.
        if (category.HasValue()
         && Categories.DoesNotContains(category))
        {
            Categories.Add(category);
            await _settingsService.AddOptionAsync(SettingType.Categories
                                                , category);
        }

        SelectedCategory = category;


        // if (category.HasValue()
        //  && Categories.NotContains(category))
        // {
        //     Categories.Add(category);
        //     await _settingsService.AddCategoryAsync(category);
        // }

        // SelectedCategory = category;
    }

    public async Task LoadItem (string? watchItemId)
    {
        if (_isLoaded) return;
        _isLoaded = true;

        await InitializeAsync(); // Ensure categories and streaming services are loaded first

        if (watchItemId?.IsEmptyNullOrWhiteSpace() ?? true)
        {
            // New item mode: initialize a new WatchItem.
            EditableItem = new WatchItem
                           {
                                   Category         = Categories.FirstOrDefault() ?? ""
                                 , StreamingService = StreamingServices.FirstOrDefault() ?? ""
                                 , Type             = AvailableTypes.FirstOrDefault() ?? ""
                           };

            OriginalItem = new WatchItem
                           {
                                   Category         = EditableItem.Category
                                 , StreamingService = EditableItem.StreamingService
                                 , Type             = EditableItem.Type
                           };

            Debug.WriteLine($"New Item Mode - Category: {EditableItem.Category}, StreamingService: {EditableItem.StreamingService}");
        }
        else
        {
            var item = _watchListService.GetWatchItems()
                                        .FirstOrDefault(watchItem => watchItem.Id
                                                                              .ToString() == watchItemId);

            if (item == null) return;

            OriginalItem = new WatchItem
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

            EditableItem = new WatchItem
                           {
                                   Id               = OriginalItem.Id
                                 , Title            = OriginalItem.Title
                                 , StreamingService = OriginalItem.StreamingService
                                 , Category         = OriginalItem.Category
                                 , DeepLinkUri      = OriginalItem.DeepLinkUri
                                 , LastUpdated      = OriginalItem.LastUpdated
                                 , IsWatched        = OriginalItem.IsWatched
                                 , IsLiked          = OriginalItem.IsLiked
                                 , Type             = OriginalItem.Type
                           };

            // Sync VM properties
            IsWatched        = EditableItem.IsWatched;
            IsLiked          = EditableItem.IsLiked;
            SelectedType     = EditableItem.Type;
            SelectedCategory = EditableItem.Category;

            Debug.WriteLine($"Loaded Item - Category: {EditableItem.Category}, StreamingService: {EditableItem.StreamingService}");
        }

        // Ensure UI updates properly
        Debug.WriteLine($"EditableItem.StreamingService BEFORE PropertyChanged: {EditableItem.StreamingService}");
        OnPropertyChanged(nameof(EditableItem));
        OnPropertyChanged(nameof(EditableItem.StreamingService));
        Debug.WriteLine($"EditableItem.StreamingService AFTER PropertyChanged: {EditableItem.StreamingService}");

//        OnPropertyChanged(nameof(EditableItem));
        OnPropertyChanged(nameof(EditableItem.Category));
//        OnPropertyChanged(nameof(EditableItem.StreamingService));

        // await InitializeAsync();
        //
        // if (watchItemId?.IsEmptyNullOrWhiteSpace() ?? true)
        // {
        //     // New item mode: initialize a new WatchItem.
        //     EditableItem = new WatchItem
        //                    {
        //                            Category         = Categories.FirstOrDefault() ?? ""
        //                          , StreamingService = StreamingServices.FirstOrDefault() ?? ""
        //                          , Type             = AvailableTypes.FirstOrDefault() ?? ""
        //                    };
        //
        //     OriginalItem = EditableItem;
        //
        //     return;
        // }
        //
        // var item = _watchListService.GetWatchItems()
        //                             .FirstOrDefault(watchItem => watchItem.Id
        //                                                                   .ToString()
        //                                                       == watchItemId);
        //
        // if (item == null) return;
        //
        // OriginalItem = item;
        // EditableItem = new WatchItem
        //                {
        //                        Id               = item.Id
        //                      , Title            = item.Title
        //                      , StreamingService = item.StreamingService
        //                      , Category         = item.Category
        //                      , DeepLinkUri      = item.DeepLinkUri
        //                      , LastUpdated      = item.LastUpdated
        //                      , IsWatched        = item.IsWatched
        //                      , IsLiked          = item.IsLiked
        //                      , Type             = item.Type
        //                };
        //
        // // Sync VM properties
        // IsWatched        = item.IsWatched;
        // IsLiked          = item.IsLiked;
        // SelectedType     = item.Type;
        // SelectedCategory = item.Category;
    }
}
