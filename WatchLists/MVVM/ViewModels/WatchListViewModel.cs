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

public partial class WatchListViewModel : ObservableObject, IDisposable
{
    private readonly WatchListService _watchListService;
    private readonly SettingsService  _settingsService;
    private readonly EventHandler    _onWatchListChanged;
    private bool                      _disposed;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    public partial int WatchedCount { get; set; }

    [ObservableProperty]
    public partial int UnwatchedCount { get; set; }

    [ObservableProperty]
    public partial bool IsSelectionMode { get; set; }

    [ObservableProperty]
    public partial int SelectedItemsCount { get; set; }

    public ObservableCollection<WatchItemGroup> WatchItemGroups         { get; set; } = new();
    public ObservableCollection<WatchItemGroup> FilteredWatchItemGroups { get; set; } = new();
    public ObservableCollection<WatchItem>      VisibleWatchItems       { get; set; } = new();
    public ObservableCollection<WatchItem>      WatchItems              { get; set; } = new();

    public Action? UpdateVisibleItemsAction { get; set; }

    public WatchListViewModel(WatchListService watchListService, SettingsService settingsService)
    {
        _watchListService = watchListService;
        _settingsService  = settingsService;
        UpdateVisibleItemsAction = UpdateVisibleItems;

        _onWatchListChanged = (sender, args) =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await LoadGroupedWatchItemsAsync();
            });
        };

        _watchListService.WatchListChanged += _onWatchListChanged;

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
    public void ToggleSelectionMode()
    {
        IsSelectionMode = !IsSelectionMode;
        if (!IsSelectionMode)
        {
            ClearSelection();
        }
        else
        {
            UpdateSelectedCount();
        }
    }

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var group in WatchItemGroups)
        {
            foreach (var item in group.Items)
            {
                item.IsSelected = true;
            }
        }
        UpdateSelectedCount();
    }

    [RelayCommand]
    public void ClearSelection()
    {
        foreach (var group in WatchItemGroups)
        {
            foreach (var item in group.Items)
            {
                item.IsSelected = false;
            }
        }
        UpdateSelectedCount();
    }

    [RelayCommand]
    public void ToggleItemSelection(WatchItem item)
    {
        if (item == null) return;
        item.IsSelected = !item.IsSelected;
        UpdateSelectedCount();
    }

    [RelayCommand]
    public void DeleteSelectedItems()
    {
        var selectedIds = WatchItemGroups.SelectMany(group => group.Items)
                                         .Where(item => item.IsSelected)
                                         .Select(item => item.Id)
                                         .ToList();

        if (selectedIds.Count == 0) return;

        _watchListService.DeleteWatchItems(selectedIds);
        IsSelectionMode = false;
        ClearSelection();
    }

    private void UpdateSelectedCount()
    {
        SelectedItemsCount = WatchItemGroups.SelectMany(group => group.Items)
                                            .Count(item => item.IsSelected);
    }

    [RelayCommand]
    public async Task OpenDeepLink(WatchItem item)
    {
        if (item == null || item.DeepLinkUri.IsEmptyNullOrWhiteSpace())
            return;

        try
        {
            await Launcher.OpenAsync(item.DeepLinkUri);
        }
        catch (Exception ex)
        {
            await FileLogger.WriteLogAsync($"Failed to launch deep link: {ex.Message}");
        }
    }

    [RelayCommand]
    public void DeleteItem(WatchItem item)
    {
        if (item == null) return;

        _watchListService.DeleteWatchItem(item.Id);

        // Remove item from group
        var group = WatchItemGroups.FirstOrDefault(targetGroup => targetGroup.Items.Contains(item));
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
        if (item == null) return;

        if (! item.IsWatched)
        {
            item.PreviousCategory = item.Category;
        }

        await Shell.Current.GoToAsync($"EditWatchItemPage?watchItemId={item.Id}");
    }

    public ObservableCollection<CategoryStatCardItem> CategoryStats { get; } = new();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watchListService.WatchListChanged -= _onWatchListChanged;
    }

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
