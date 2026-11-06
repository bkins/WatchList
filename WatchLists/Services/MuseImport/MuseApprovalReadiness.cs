namespace WatchLists.Services.MuseImport;

public static class MuseApprovalReadiness
{
    public static string? GetMessage(bool reviewStarted
                                   , bool rowSelected
                                   , bool rowReadable
                                   , bool matchSelected
                                   , bool existingItem)
    {
        if (!reviewStarted) return "Choose a Muse file first.";
        if (!rowSelected) return "Select a recommendation in the list above first.";
        if (!rowReadable) return "This recommendation cannot be reviewed because saved items could not be read.";
        if (!matchSelected)
        {
            return existingItem
                ? "Choose an item under 'Already in your WatchList' first."
                : "Choose a suggestion under 'Add a new item' first. Wait for suggestions or search again if the list is empty.";
        }
        return null;
    }
}
