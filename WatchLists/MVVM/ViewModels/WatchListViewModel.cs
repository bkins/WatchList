using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchLists.ExtensionMethods;
using WatchLists.Logger;
using WatchLists.MVVM.Models;
using WatchLists.MVVM.Views;
using WatchLists.Services;
using WatchLists.Services.Enums;

namespace WatchLists.MVVM.ViewModels;

public partial class WatchListViewModel : ObservableObject
{
    private readonly WatchListService _watchListService;
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    private string searchText;

    [ObservableProperty]
    private int totalCount;

    [ObservableProperty]
    private int watchedCount;

    [ObservableProperty]
    private int unwatchedCount;

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
    private async Task NavigateToLogs()
    {
        await Shell.Current.GoToAsync(nameof(LogsPage));
    }

    [RelayCommand]
    private async Task NavigateToApiTest()
    {
        await Shell.Current.GoToAsync(nameof(ApiTestPage));
    }

    [RelayCommand]
    public async Task OpenDeepLink(WatchItem item)
    {
        if (item.DeepLinkUri.IsEmptyNullOrWhiteSpace())
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
        if (! item.IsWatched)
        {
            item.PreviousCategory = item.Category;
        }

        await Shell.Current.GoToAsync($"EditWatchItemPage?watchItemId={item.Id}");
    }

    public ObservableCollection<CategoryStatCardItem> CategoryStats { get; } = new();

    private async Task LoadGroupedWatchItemsAsync()
    {
        var managedCategories = await _settingsService.GetOptionsAsync(SettingType.Categories);

        var allWatchItems = _watchListService.GetWatchItems();
        var groupedItems = allWatchItems.GroupBy(item => item.Category.HasValue() ? item.Category : "Currently Watching").ToList();

        // Sort groups based on managed category order.
        var sortedGroups = groupedItems.OrderBy(grouping =>
        {
            var index = managedCategories.IndexOf(grouping.Key);
            return index >= 0 ? index : int.MaxValue;
        });

        WatchItemGroups.Clear();
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

        var allItems = WatchItemGroups.SelectMany(g => g.Items).ToList();
        TotalCount = allItems.Count;
        WatchedCount = allItems.Count(i => i.IsWatched);
        UnwatchedCount = TotalCount - WatchedCount;

        CategoryStats.Clear();
        CategoryStats.Add(new CategoryStatCardItem
        {
            CategoryName = "All",
            Count = TotalCount,
            IsSelected = true,
            SelectCommand = new RelayCommand(() => SelectCategoryStat("All"))
        });

        foreach (var group in WatchItemGroups.Where(g => g.Items.Count > 0))
        {
            var catName = group.CategoryName;
            CategoryStats.Add(new CategoryStatCardItem
            {
                CategoryName = catName,
                Count = group.Items.Count,
                IsSelected = false,
                SelectCommand = new RelayCommand(() => SelectCategoryStat(catName))
            });
        }

        await FileLogger.WriteLogAsync($"Loaded {groupedItems.Count} watch items");
    }

    [RelayCommand]
    public void SelectCategoryStat(string categoryName)
    {
        foreach (var stat in CategoryStats)
        {
            stat.IsSelected = stat.CategoryName.EqualsIgnoreCase(categoryName);
        }

        if (categoryName.EqualsIgnoreCase("All"))
        {
            foreach (var group in WatchItemGroups)
            {
                group.IsExpanded = true;
            }
        }
        else
        {
            foreach (var group in WatchItemGroups)
            {
                group.IsExpanded = group.CategoryName.EqualsIgnoreCase(categoryName);
            }
        }

        UpdateVisibleItemsAction?.Invoke();
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterGroups();
    }

    [RelayCommand]
    private void FilterItems()
    {
        FilterGroups();
    }

    private void FilterGroups()
    {
        if (SearchText.IsEmptyNullOrWhiteSpace())
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

public partial class CategoryStatCardItem : ObservableObject
{
    [ObservableProperty] private string categoryName = string.Empty;
    [ObservableProperty] private int count;
    [ObservableProperty] private bool isSelected;
    public System.Windows.Input.ICommand SelectCommand { get; set; }
}
