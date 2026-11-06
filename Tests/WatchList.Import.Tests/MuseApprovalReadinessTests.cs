using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MuseApprovalReadinessTests
{
    [Fact]
    public void GetMessage_NoRowSelected_ExplainsSelection()
    {
        var result = MuseApprovalReadiness.GetMessage(true, false, false, false, false);

        Assert.Contains("Select a recommendation", result);
    }

    [Fact]
    public void GetMessage_NoProviderSelection_ExplainsHowToChoose()
    {
        var result = MuseApprovalReadiness.GetMessage(true, true, true, false, false);

        Assert.Contains("Choose a suggestion", result);
    }

    [Fact]
    public void GetMessage_NoExistingSelection_ExplainsWhichControlToUse()
    {
        var result = MuseApprovalReadiness.GetMessage(true, true, true, false, true);

        Assert.Contains("Already in your WatchList", result);
    }

    [Fact]
    public void GetMessage_ValidSelection_AllowsApproval()
    {
        var result = MuseApprovalReadiness.GetMessage(true, true, true, true, false);

        Assert.Null(result);
    }
    [Fact]
    public void GetMessage_ReviewUnavailable_ExplainsWhyApprovalIsBlocked()
    {
        var result = MuseApprovalReadiness.GetMessage(true, true, false, true, false);

        Assert.Contains("could not be read", result);
    }

    [Fact]
    public void GetMessage_NoFileLoaded_RequestsFile()
    {
        var result = MuseApprovalReadiness.GetMessage(false, false, false, false, false);

        Assert.Contains("Choose a Muse file", result);
    }
}
