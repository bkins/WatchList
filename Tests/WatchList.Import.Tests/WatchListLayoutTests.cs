using WatchLists.MVVM.Views;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class WatchListLayoutTests
{
    [Theory]
    [InlineData(240, 96)]
    [InlineData(400, 160)]
    [InlineData(600, 240)]
    [InlineData(800, 320)]
    [InlineData(1600, 320)]
    public void GetHeaderMaximumHeight_ValidPageHeight_ReservesSpaceForTheList(double pageHeight, double expectedHeight)
    {
        var maximumHeight = WatchListLayout.GetHeaderMaximumHeight(pageHeight);

        Assert.Equal(expectedHeight, maximumHeight);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void GetHeaderMaximumHeight_UnavailablePageHeight_UsesFiniteMinimum(double pageHeight)
    {
        var maximumHeight = WatchListLayout.GetHeaderMaximumHeight(pageHeight);

        Assert.Equal(96, maximumHeight);
    }
}
