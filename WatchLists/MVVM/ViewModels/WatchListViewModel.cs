using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchLists.ExtensionMethods;
using WatchLists.Logger;
using WatchLists.MVVM.Models;
using WatchLists.MVVM.Views;
using WatchLists.Services;

namespace WatchLists.MVVM.ViewModels;

public partial class WatchListViewModel : ObservableObject
{
    private readonly WatchListService _watchListService;
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    private string searchText;

    public ObservableCollection<WatchItemGroup> WatchItemGroups { get; set; } = new();
    public ObservableCollection<WatchItemGroup> FilteredWatchItemGroups { get; set; } = new();
    public ObservableCollection<WatchItem> VisibleWatchItems { get; set; } = new();
    public ObservableCollection<WatchItem> WatchItems { get; set; } = new();

    public Action UpdateVisibleItemsAction { get; set; }

    public WatchListViewModel(WatchListService watchListService, SettingsService settingsService)
    {
        _watchListService = watchListService;
        _settingsService = settingsService;
        UpdateVisibleItemsAction = UpdateVisibleItems;

        // Start loading grouped items asynchronously.
        _ = LoadGroupedWatchItemsAsync();
    }

    [RelayCommand]
    private async Task RefreshItems()
    {
        await LoadGroupedWatchItemsAsync();
    }

    [RelayCommand]
    private async Task AddItem()
    {
        await Shell.Current.GoToAsync(nameof(EditWatchItemPage));
    }

    [RelayCommand]
    public async Task NavigateToSettings()
    {
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }

    [RelayCommand]
    public async Task OpenDeepLink(WatchItem item)
    {
        if (string.IsNullOrWhiteSpace(item.DeepLinkUri))
            return;

        try
        {
            await Launcher.OpenAsync(item.DeepLinkUri);
        }
        catch (Exception ex)
        {
            await FileLogger.WriteLogAsync($"Failed to launch deep link: {ex.Message}");
            //Debug.WriteLine($"Failed to launch deep link: {ex.Message}");
        }
    }

    [RelayCommand]
    public void DeleteItem(WatchItem item)
    {
        _watchListService.DeleteWatchItem(item.Id);

        // Remove item from group
        var group = WatchItemGroups.FirstOrDefault(g => g.Items.Contains(item));
        if (group != null)
        {
            group.Items.Remove(item);
            if (group.Items.Count == 0)
                WatchItemGroups.Remove(group);
        }
    }

    [RelayCommand]
    public async Task EditItem(WatchItem item)
    {
        await Shell.Current.GoToAsync($"EditWatchItemPage?watchItemId={item.Id}");
    }

    [RelayCommand]
    private async Task NavigateToLogs()
    {
        await Shell.Current.GoToAsync(nameof(LogsPage));
    }

    private async Task LoadGroupedWatchItemsAsync()
    {
        var managedCategories = await _settingsService.LoadOptionsAsync("Categories.json",
                                                    "Currently Watching,Finished Watching,Consider Watching");

        WatchItemGroups.Clear();
        var groupedItems = _watchListService.GetWatchItems().GroupBy(item => item.Category).ToList();

        // Sort groups based on managed category order.
        var sortedGroups = groupedItems.OrderBy(grouping =>
        {
            var index = managedCategories.IndexOf(grouping.Key);
            return index >= 0 ? index : int.MaxValue;
        });

        foreach (var group in sortedGroups)
        {
            var watchItemGroup = new WatchItemGroup(group.Key);
            watchItemGroup.ToggleExpandCommand = new RelayCommand<WatchItemGroup>((_) =>
            {
                watchItemGroup.IsExpanded = !watchItemGroup.IsExpanded;
                UpdateVisibleItemsAction?.Invoke();
            });

            foreach (var item in group)
            {
                watchItemGroup.Items.Add(item);
            }

            WatchItemGroups.Add(watchItemGroup);
        }

        FilterGroups();
        UpdateVisibleItems();
        await FileLogger.WriteLogAsync($"Loaded {groupedItems.Count} watch items");
        // Debug.WriteLine($"Loaded {groupedItems.Count} watch items");
    }

    private void FilterGroups()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredWatchItemGroups.Clear();
            foreach (var group in WatchItemGroups)
            {
                FilteredWatchItemGroups.Add(group);
            }
        }
        else
        {
            var lowerSearch = SearchText.ToLowerInvariant();
            var filtered = new ObservableCollection<WatchItemGroup>();

            foreach (var group in WatchItemGroups)
            {
                var filteredItems = group.Items
                                         .Where(item => item.Title.HasValue() &&
                                                        item.Title.ToLowerInvariant().Contains(lowerSearch))
                                         .ToList();

                if (filteredItems.Count > 0)
                {
                    var newGroup = new WatchItemGroup(group.CategoryName)
                    {
                        IsExpanded = group.IsExpanded
                    };
                    foreach (var item in filteredItems)
                    {
                        newGroup.Items.Add(item);
                    }
                    filtered.Add(newGroup);
                }
            }

            FilteredWatchItemGroups.Clear();
            foreach (var group in filtered)
            {
                FilteredWatchItemGroups.Add(group);
            }
        }

        OnPropertyChanged(nameof(FilteredWatchItemGroups));
    }

    private void UpdateVisibleItems()
    {
        VisibleWatchItems.Clear();
        foreach (var group in WatchItemGroups)
        {
            if (!group.IsExpanded)
            {
                foreach (var item in group.Items)
                {
                    VisibleWatchItems.Add(item);
                }
            }
        }
    }
}
