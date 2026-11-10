using WatchLists.Services;
using WatchLists.Services.MuseImport;

namespace WatchLists.MVVM.Views;

internal sealed class RecommendationPanel : VerticalStackLayout
{
    private readonly ContentPage _page;
    private readonly WatchListService _service;
    private readonly Guid _itemId;
    private readonly long _generation;
    private readonly Action _refresh;
    private readonly MovieRecommendation[] _recommendations;

    public RecommendationPanel(ContentPage page, WatchListService service, Guid itemId, long generation, Action refresh)
    {
        _page            = page;
        _service         = service;
        _itemId          = itemId;
        _generation      = generation;
        _refresh         = refresh;
        _recommendations = service.GetRecommendations(itemId, generation);
        Spacing = 8;

        var primary = RecommendationSet.Primary(_recommendations);
        if (primary is not null)
        {
            var heading = new Label { Text = "Why I Saved This", FontSize = 20, FontAttributes = FontAttributes.Bold };
            SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level2);
            Children.Add(heading);
            AddRecommendation(primary, true);
            foreach (var recommendation in _recommendations.Where(row => row.Id != primary.Id))
                AddRecommendation(recommendation, false);
        }

        Children.Add(ActionButton("Add recommendation", () => EditAsync(null)));
    }

    private void AddRecommendation(MovieRecommendation recommendation, bool primary)
    {
        var name = RecommendationFields.Name(recommendation);
        var values = RecommendationMetadata.Effective(recommendation);
        var sentiment = values.GetValueOrDefault(RecommendationField.Sentiment);
        var suffix = sentiment is null or "Neutral or unknown" ? string.Empty : $" · {sentiment}";
        Children.Add(new Label
                     {
                         Text           = (primary ? $"Primary · {name}" : name) + suffix
                       , FontAttributes = primary ? FontAttributes.Bold : FontAttributes.None
                     });

        var spoilers = RecommendationMetadata.HasSpoilers(recommendation);
        if (primary)
        {
            var preview = spoilers ? "This recommendation contains spoilers." : PrimaryPreview(values);
            if (preview.Length > 0)
                Children.Add(new Label { Text = preview, MaxLines = 5, LineBreakMode = LineBreakMode.TailTruncation });
        }

        var details = new VerticalStackLayout { IsVisible = false, Spacing = 8 };
        var loaded = false;
        var button = ActionButton("Recommendation details", async () =>
        {
            if (!loaded)
            {
                if (spoilers && !await _page.DisplayAlertAsync("Spoiler warning", "Reveal this recommendation?", "Reveal", "Cancel"))
                    return;
                AddDetails(details, recommendation, values, primary);
                loaded = true;
            }
            details.IsVisible = !details.IsVisible;
        });
        SemanticProperties.SetDescription(button, $"Recommendation details for {name}");
        Children.Add(button);
        Children.Add(details);
    }

    private void AddDetails(VerticalStackLayout details, MovieRecommendation recommendation
                          , IReadOnlyDictionary<RecommendationField, string> values, bool primary)
    {
        foreach (var (field, value) in values.OrderBy(field => field.Key))
        {
            if (field == RecommendationField.ContainsSpoilers || string.IsNullOrWhiteSpace(value)) continue;
            details.Children.Add(new Label { Text = $"{RecommendationFields.Label(field)}\n{value}" });
        }
        if (values.TryGetValue(RecommendationField.SourceUrl, out var url) && !string.IsNullOrWhiteSpace(url))
            details.Children.Add(ActionButton("Open source", async () => await Launcher.Default.OpenAsync(new Uri(url))));
        details.Children.Add(ActionButton("Edit recommendation", () => EditAsync(recommendation)));
        if (!primary)
        {
            details.Children.Add(ActionButton("Make primary", () =>
            {
                _service.SetPrimaryRecommendation(_itemId, _generation, recommendation.Id);
                _refresh();
                return Task.CompletedTask;
            }));
        }
        details.Children.Add(ActionButton("Delete recommendation", () => DeleteAsync(recommendation, primary)));
    }

    private async Task DeleteAsync(MovieRecommendation recommendation, bool primary)
    {
        Guid? replacementId = null;
        if (primary && _recommendations.Length > 1)
        {
            var others = _recommendations.Where(row => row.Id != recommendation.Id).ToArray();
            var choices = others.Select((row, index) => $"{index + 1}. {RecommendationFields.Name(row)}").ToArray();
            var selected = await _page.DisplayActionSheetAsync("Choose the replacement primary", "Cancel", null, choices);
            var index = Array.IndexOf(choices, selected);
            if (index < 0) return;
            replacementId = others[index].Id;
        }
        if (!await _page.DisplayAlertAsync("Delete recommendation?", "Remove this recommendation from the saved item?", "Delete", "Cancel"))
            return;
        _service.RemoveRecommendation(_itemId, _generation, recommendation.Id, replacementId);
        _refresh();
    }

    private Task EditAsync(MovieRecommendation? recommendation)
    {
        var editor = new RecommendationEditorPage(_service, _itemId, _generation, recommendation, _refresh);
        return _page.Navigation.PushModalAsync(new NavigationPage(editor));
    }

    private Button ActionButton(string text, Func<Task> action)
    {
        var button = new Button { Text = text };
        button.SetDynamicResource(StyleProperty, "SecondaryButtonStyle");
        button.Clicked += async (_, _) =>
        {
            button.IsEnabled = false;
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                await _page.DisplayAlertAsync("Recommendation unavailable", exception.Message, "OK");
            }
            finally
            {
                button.IsEnabled = true;
            }
        };
        return button;
    }

    private static string PrimaryPreview(IReadOnlyDictionary<RecommendationField, string> values)
    {
        var preview = new[] { RecommendationField.Context, RecommendationField.Rationale, RecommendationField.Commentary }
            .Select(field => values.GetValueOrDefault(field))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        return preview.Length > 0 ? string.Join("\n", preview) : values.GetValueOrDefault(RecommendationField.BasicDescription)
                                                               ?? values.GetValueOrDefault(RecommendationField.SourceUrl)
                                                               ?? string.Empty;
    }
}
