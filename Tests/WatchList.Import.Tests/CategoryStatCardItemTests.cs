using WatchLists.MVVM.ViewModels;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class CategoryStatCardItemTests
{
    [Fact]
    public void Properties_ChangedAfterConstruction_RaiseBindableNotifications()
    {
        var item = new CategoryStatCardItem();
        var notifications = new List<string?>();
        item.PropertyChanged += (sender, eventArgs) => notifications.Add(eventArgs.PropertyName);

        item.CategoryName = "Synthetic";
        item.Count = 3;
        item.IsSelected = true;

        Assert.Equal(new[] { "CategoryName", "DisplayText", "Count", "DisplayText", "IsSelected", "DisplayText" }, notifications);
        Assert.Equal("Synthetic", item.CategoryName);
        Assert.Equal(3, item.Count);
        Assert.True(item.IsSelected);
        Assert.Equal("Synthetic · 3 · selected", item.DisplayText);
    }
}
