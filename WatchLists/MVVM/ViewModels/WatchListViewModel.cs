using System.Collections.ObjectModel;
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
    private bool                      _loadInProgress;
    private bool                      _reloadRequested;
    private ObservableCollection<WatchItemGroup> _watchItemGroups = new();
    private ObservableCollection<WatchItemGroup> _filteredWatchItemGroups = new();
    private ObservableCollection<CategoryStatCardItem> _categoryStats = new();

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

    [ObservableProperty]
    public partial bool IsLoadingItems { get; set; } = true;

    public ObservableCollection<WatchItemGroup> WatchItemGroups
    {
        get => _watchItemGroups;
        private set => SetProperty(ref _watchItemGroups, value);
    }

    public ObservableCollection<WatchItemGroup> FilteredWatchItemGroups
    {
        get => _filteredWatchItemGroups;
        private set => SetProperty(ref _filteredWatchItemGroups, value);
    }

    public WatchListViewModel(WatchListService watchListService, SettingsService settingsService)
    {
        _watchListService = watchListService;
        _settingsService  = settingsService;
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

        await Shell.Current.GoToAsync($"EditWatchItemPage?watchItemId={item.Id}");
    }

    public ObservableCollection<CategoryStatCardItem> CategoryStats
    {
        get => _categoryStats;
        private set => SetProperty(ref _categoryStats, value);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watchListService.WatchListChanged -= _onWatchListChanged;
    }

    private async Task LoadGroupedWatchItemsAsync()
    {
        if (_loadInProgress)
        {
            _reloadRequested = true;
            return;
        }

        _loadInProgress = true;
        IsLoadingItems = true;
        try
        {
            do
            {
                _reloadRequested = false;
                await Task.Yield();
                var managedCategories = await _settingsService.GetOptionsAsync(SettingType.Categories);
                var loadedData = await Task.Run(() => BuildGroupedWatchItems(managedCategories));

                WatchItemGroups = new ObservableCollection<WatchItemGroup>(loadedData.Groups);

                FilterGroups();
                TotalCount = loadedData.TotalCount;
                WatchedCount = loadedData.WatchedCount;
                UnwatchedCount = TotalCount - WatchedCount;
                RefreshCategoryStats();

                await FileLogger.WriteLogAsync($"Loaded {loadedData.GroupCount} watch item groups");
            }
            while (_reloadRequested);
        }
        finally
        {
            _loadInProgress = false;
            IsLoadingItems = false;
        }
    }

    private (List<WatchItemGroup> Groups, int TotalCount, int WatchedCount, int GroupCount)
        BuildGroupedWatchItems(List<string> managedCategories)
    {
        var allWatchItems = _watchListService.GetWatchItems();
        var unverifiedItems = _watchListService.GetUnverifiedMuseItems();
        var groupedItems = allWatchItems
            .GroupBy(item => item.Category.HasValue() ? item.Category : "Currently Watching")
            .OrderBy(grouping =>
            {
                var index = managedCategories.IndexOf(grouping.Key);
                return index >= 0 ? index : int.MaxValue;
            })
            .ToList();
        var groups = new List<WatchItemGroup>();

        foreach (var grouping in groupedItems)
        {
            var group = new WatchItemGroup(grouping.Key);
            foreach (var item in grouping)
            {
                group.Items.Add(item);
            }
            groups.Add(group);
        }

        if (unverifiedItems.Count > 0 || managedCategories.Contains("Unverified", StringComparer.OrdinalIgnoreCase))
        {
            var unverifiedGroup = groups.FirstOrDefault(group =>
                string.Equals(group.CategoryName, "Unverified", StringComparison.OrdinalIgnoreCase)) ?? new WatchItemGroup("Unverified");
            foreach (var item in unverifiedItems)
            {
                unverifiedGroup.UnverifiedItems.Add(item);
            }
            if (!groups.Contains(unverifiedGroup))
            {
                groups.Add(unverifiedGroup);
            }
        }

        return (groups, allWatchItems.Count, allWatchItems.Count(item => item.IsWatched), groupedItems.Count);
    }

    private void RefreshCategoryStats()
    {
        var categoryStats = new ObservableCollection<CategoryStatCardItem>();
        categoryStats.Add(new CategoryStatCardItem
        {
            CategoryName = "All"
          , Count       = TotalCount
          , IsSelected  = true
          , SelectCommand = new RelayCommand(() => SelectCategoryStat("All"))
        });

        foreach (var group in WatchItemGroups.Where(itemGroup => itemGroup.Items.Count > 0))
        {
            var categoryName = group.CategoryName;
            categoryStats.Add(new CategoryStatCardItem
            {
                CategoryName = categoryName
              , Count       = group.Items.Count
              , IsSelected  = false
              , SelectCommand = new RelayCommand(() => SelectCategoryStat(categoryName))
            });
        }

        CategoryStats = categoryStats;
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
        var filteredGroups = new List<WatchItemGroup>();
        if (SearchText.IsEmptyNullOrWhiteSpace())
        {
            filteredGroups.AddRange(WatchItemGroups);
        }
        else
        {
            var query = SearchText.Trim();
            var recommendations = _watchListService.GetRecommendationSearchIndex();

            foreach (var group in WatchItemGroups)
            {
                var filteredItems = group.Items
                                         .Where(item => item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                                                     || recommendations[item.Id].Any(value => value.Contains(query, StringComparison.OrdinalIgnoreCase)))
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
                    filteredGroups.Add(newGroup);
                }
            }
        }

        FilteredWatchItemGroups = new ObservableCollection<WatchItemGroup>(filteredGroups);
    }

}
