using WatchLists.Services.MuseImport;

namespace WatchLists.MVVM.Views;

public partial class MuseRecommendationDetailsPage : ContentPage
{
    public MuseRecommendationDetailsPage(MuseRecommendation recommendation, string savedAssociations)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        InitializeComponent();
        BindingContext = recommendation with
        {
            SourceUrls = recommendation.SourceUrls
                .Where(sourceUrl => Uri.TryCreate(sourceUrl, UriKind.Absolute, out _))
                .ToArray()
        };
        SavedAssociationsText.Text = savedAssociations;
        SavedAssociationsSection.IsVisible = !string.IsNullOrWhiteSpace(savedAssociations);
    }

    private async void OpenSource(object? sender, TappedEventArgs eventArgs)
    {
        await OpenSourceAsync(sender);
    }

    private async void OpenSourceButton(object? sender, EventArgs eventArgs)
    {
        await OpenSourceAsync(sender);
    }

    private async Task OpenSourceAsync(object? sender)
    {
        if (!TryGetSourceUrl(sender, out var sourceUri) || sourceUri == null) return;
        try
        {
            await Launcher.OpenAsync(sourceUri);
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("Could not open Muse source", exception.Message, "OK");
        }
    }

    private async void CopySource(object? sender, EventArgs eventArgs)
    {
        if (!TryGetSourceUrl(sender, out var sourceUri) || sourceUri == null) return;
        try
        {
            await Clipboard.Default.SetTextAsync(sourceUri.AbsoluteUri);
            await DisplayAlertAsync("Link copied", "The Muse source link is on the clipboard.", "OK");
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("Could not copy Muse source", exception.Message, "OK");
        }
    }

    private static bool TryGetSourceUrl(object? sender, out Uri? sourceUri)
    {
        sourceUri = null;
        if (sender is not BindableObject { BindingContext: string sourceUrl } ||
            !Uri.TryCreate(sourceUrl, UriKind.Absolute, out var parsedSourceUri) || parsedSourceUri == null) return false;
        sourceUri = parsedSourceUri;
        return true;
    }

    private async void CloseDetails(object? sender, EventArgs eventArgs)
    {
        await Navigation.PopModalAsync();
    }
}
