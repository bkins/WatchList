using WatchLists.Services;
using WatchLists.Services.MuseImport;

namespace WatchLists.MVVM.Views;

internal sealed class RecommendationEditorPage : ContentPage
{
    public RecommendationEditorPage(WatchListService service, Guid itemId, long generation, MovieRecommendation? recommendation
                                   , Action refresh)
    {
        Title = recommendation is null ? "Add recommendation" : "Edit recommendation";
        var values = recommendation is null ? [] : RecommendationMetadata.Effective(recommendation);
        var original = recommendation is null ? [] : RecommendationMetadata.Read(recommendation.EditedMetadata);
        var inputs = new Dictionary<RecommendationField, Func<string>>();
        var layout = new VerticalStackLayout { Padding = 16, Spacing = 10 };

        foreach (var field in Enum.GetValues<RecommendationField>())
        {
            var label = RecommendationFields.Label(field);
            var value = values.GetValueOrDefault(field) ?? string.Empty;
            layout.Children.Add(new Label { Text = label });
            View input;
            if (field == RecommendationField.Sentiment)
            {
                var picker = new Picker
                             {
                                 ItemsSource  = RecommendationMetadata.Sentiments.ToArray()
                               , SelectedItem = value.Length == 0 ? RecommendationMetadata.Sentiments[0] : value
                             };
                input = picker;
                inputs.Add(field, () => picker.SelectedItem as string ?? RecommendationMetadata.Sentiments[0]);
            }
            else if (field == RecommendationField.ContainsSpoilers)
            {
                var toggle = new Switch { IsToggled = value == "true" };
                input = toggle;
                inputs.Add(field, () => toggle.IsToggled ? "true" : "false");
            }
            else
            {
                var editor = new Editor { Text = value, AutoSize = EditorAutoSizeOption.TextChanges };
                if (field == RecommendationField.SourceUrl) editor.Keyboard = Keyboard.Url;
                input = editor;
                inputs.Add(field, () => editor.Text?.Trim() ?? string.Empty);
            }
            SemanticProperties.SetDescription(input, label);
            input.AutomationId = $"RecommendationField{field}";
            layout.Children.Add(input);
        }

        var save = new Button { Text = "Save recommendation", AutomationId = "SaveRecommendation" };
        save.Clicked += async (_, _) =>
        {
            save.IsEnabled = false;
            try
            {
                foreach (var (field, read) in inputs)
                {
                    var previous = values.GetValueOrDefault(field) ?? (field switch
                    {
                        RecommendationField.Sentiment         => RecommendationMetadata.Sentiments[0]
                      , RecommendationField.ContainsSpoilers  => "false"
                      , _                                     => string.Empty
                    });
                    var next = read();
                    if (next != previous) original[field] = next;
                }
                service.SaveRecommendation(itemId, generation, recommendation, original);
                await Navigation.PopModalAsync();
                refresh();
            }
            catch (Exception exception)
            {
                await DisplayAlertAsync("Recommendation could not be saved", exception.Message, "OK");
            }
            finally
            {
                save.IsEnabled = true;
            }
        };
        layout.Children.Add(save);
        var cancel = new Button { Text = "Cancel" };
        cancel.Clicked += async (_, _) => await Navigation.PopModalAsync();
        layout.Children.Add(cancel);
        Content = new ScrollView { Content = layout };
    }
}
