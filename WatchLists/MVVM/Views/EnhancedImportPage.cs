using System.Text;
using WatchLists.Services;
using WatchLists.Services.MuseImport;

namespace WatchLists.MVVM.Views;

internal sealed class EnhancedImportPage : ContentPage
{
    private const int RowsPerPage = 12;
    private const int MaximumFileCharacters = 4_000_000;
    private readonly WatchListService _service;
    private readonly Label _summary = new() { AutomationId = "EnhancedImportSummary" };
    private readonly Label _range = new();
    private readonly VerticalStackLayout _rows = new() { Spacing = 12 };
    private readonly ScrollView _scroll;
    private readonly Button _choose;
    private readonly Button _previous;
    private readonly Button _next;
    private readonly Button _apply;
    private readonly Dictionary<EnhancedRecommendation, IReadOnlySet<RecommendationField>> _decisions = [];
    private readonly HashSet<EnhancedRecommendation> _skipped = [];
    private EnhancedImportRow[] _entries = [];
    private EnhancedImportReview? _review;
    private long _generation;
    private int _page;
    private bool _busy;

    public EnhancedImportPage(WatchListService service)
    {
        _service  = service;
        Title     = "Import enhanced recommendations";
        _choose   = ActionButton("Choose enhanced Markdown file", ChooseAsync);
        _previous = ActionButton("Previous rows", () => MoveAsync(-1));
        _next     = ActionButton("Next rows", () => MoveAsync(1));
        _apply    = ActionButton("Apply reviewed recommendations", ApplyAsync);
        _scroll   = new ScrollView { Content = _rows };
        _choose.AutomationId = "ChooseEnhancedFile";
        _apply.AutomationId  = "ApplyEnhancedImport";

        var header = new VerticalStackLayout { Spacing = 6 };
        header.Children.Add(new Label { Text = "Review recommendations for titles already saved in WatchList." });
        header.Children.Add(_choose);
        header.Children.Add(_summary);
        header.Children.Add(_range);

        var paging = new Grid
                     {
                         ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)]
                       , ColumnSpacing     = 6
                     };
        paging.Add(_previous, 0);
        paging.Add(_next, 1);
        var footer = new VerticalStackLayout { Spacing = 6 };
        footer.Children.Add(paging);
        footer.Children.Add(_apply);
        footer.Children.Add(ActionButton("Close", async () => await Navigation.PopModalAsync()));

        var layout = new Grid
                     {
                         Padding        = 12
                       , RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)]
                       , RowSpacing     = 8
                     };
        layout.Add(header, 0, 0);
        layout.Add(_scroll, 0, 1);
        layout.Add(footer, 0, 2);
        Content = layout;
        Render();
    }

    private async Task ChooseAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose complete Muse enhanced analysis" });
        if (file is null) return;
        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        var buffer = new char[8192];
        var markdown = new StringBuilder();
        while (await reader.ReadAsync(buffer.AsMemory()) is var read && read > 0)
        {
            if (markdown.Length + read > MaximumFileCharacters)
                throw new InvalidDataException("The enhanced analysis file is too large.");
            markdown.Append(buffer, 0, read);
        }

        var entries = new EnhancedRecommendationParser().Parse(markdown.ToString());
        (_review, _generation) = _service.CreateEnhancedImportReview();
        _entries = entries.Select(entry => _review.Preview(entry)).ToArray();
        _decisions.Clear();
        _skipped.Clear();
        _page = 0;
        Render();
    }

    private async Task MatchAsync(int index)
    {
        var row = _entries[index];
        var query = await DisplayPromptAsync("Find a saved item", "Enter a title to search WatchList.", initialValue: row.Entry.Title);
        if (query is null) return;
        var candidates = _review!.Candidates(row.Entry, query);
        if (candidates.Count is 0 or > 20)
        {
            var message = candidates.Count == 0 ? "No saved item matched. Try another title or add it to WatchList first."
                                               : "More than 20 saved items matched. Enter a more specific title.";
            await DisplayAlertAsync("Choose a saved item", message, "OK");
            return;
        }

        var labels = candidates.Select((item, position) => $"{position + 1}. {item.ImportReviewDisplay}").ToArray();
        var chosen = await DisplayActionSheetAsync($"Match {row.Entry.Title}", "Cancel", null, labels);
        var match = Array.IndexOf(labels, chosen);
        if (match < 0) return;
        var target = candidates[match];
        if (!await DisplayAlertAsync("Use this match?", $"{row.Entry.Title} ({row.Entry.YearAndType})\n\n{target.ImportReviewDisplay}", "Use match", "Cancel"))
            return;

        _entries[index] = _review.Preview(row.Entry, target.Id);
        _decisions.Remove(row.Entry);
        _skipped.Remove(row.Entry);
        Render();
    }

    private async Task ReviewConflictsAsync(EnhancedImportRow row)
    {
        if ((row.Entry.Metadata.GetValueOrDefault(RecommendationField.ContainsSpoilers) == "true"
            || row.Existing is not null && RecommendationMetadata.HasSpoilers(row.Existing))
            && !await DisplayAlertAsync("Spoiler warning", "Reveal the values in this review?", "Reveal", "Cancel"))
            return;

        var approved = new HashSet<RecommendationField>();
        var original = RecommendationMetadata.Read(row.Existing?.OriginalMetadata);
        var edits    = RecommendationMetadata.Read(row.Existing?.EditedMetadata);
        foreach (var field in row.Conflicts())
        {
            var message = $"Stored source value: {original.GetValueOrDefault(field, "(empty)")}\n\nIncoming: {row.Entry.Metadata[field]}";
            if (edits.TryGetValue(field, out var edit))
                message += $"\n\nYour edit: {edit} (kept either way)";
            if (await DisplayAlertAsync(RecommendationFields.Label(field), message, "Use incoming source value", "Keep stored source value"))
                approved.Add(field);
        }
        if (!await DisplayAlertAsync("Record conflict decisions?", "Your edits stay in place. The selected source values will be applied when you confirm the import.", "Record decisions", "Cancel"))
            return;
        _decisions[row.Entry] = approved;
        Render();
    }

    private async Task ShowIncomingAsync(EnhancedRecommendation entry)
    {
        if (entry.Metadata.GetValueOrDefault(RecommendationField.ContainsSpoilers) == "true"
            && !await DisplayAlertAsync("Spoiler warning", "Reveal the incoming recommendation?", "Reveal", "Cancel"))
            return;

        var details = entry.Metadata.Where(field => field.Key != RecommendationField.ContainsSpoilers)
                                    .OrderBy(field => field.Key)
                                    .Select(field => $"{RecommendationFields.Label(field.Key)}\n{field.Value}");
        var body = string.Join("\n\n", new[] { entry.YearAndType, entry.SourceUrl }.Concat(details));
        await DisplayAlertAsync(entry.Title, body, "Close");
    }

    private async Task ApplyAsync()
    {
        var approvals = _entries.Where(Ready)
                                .Select(row => new EnhancedImportApproval(row, _decisions.GetValueOrDefault(row.Entry) ?? new HashSet<RecommendationField>()))
                                .ToArray();
        if (approvals.Length == 0) return;
        if (!await DisplayAlertAsync("Apply enhanced recommendations?", _summary.Text + "\n\nUnmatched and unreviewed rows will be skipped. WatchList will make a local backup.", "Apply", "Cancel"))
            return;

        var receipt = await _service.ApplyEnhancedImportAsync(approvals, _generation);
        var unchanged = _entries.Count(row => row.Target is not null && !row.HasChanges()) + receipt.Unchanged;
        var conflicts = _entries.Count(NeedsConflictReview);
        var unmatched = _entries.Count(row => row.Target is null);
        var skipped   = _skipped.Count;
        _entries = [];
        _decisions.Clear();
        _skipped.Clear();
        _page = 0;
        Render();
        _summary.Text = $"Added {receipt.Added} · Enriched {receipt.Enriched} · Unchanged or removed {unchanged}\n" +
                        $"Unreviewed conflicts {conflicts} · Unmatched {unmatched} · Skipped {skipped} · Failed 0";
    }

    private bool Ready(EnhancedImportRow row)
    {
        return !_skipped.Contains(row.Entry) && row.HasChanges() && !NeedsConflictReview(row);
    }

    private bool NeedsConflictReview(EnhancedImportRow row)
    {
        return !_skipped.Contains(row.Entry) && row.HasChanges() && row.Conflicts().Count > 0 && !_decisions.ContainsKey(row.Entry);
    }

    private void Render()
    {
        _rows.Children.Clear();
        var first = _page * RowsPerPage;
        var last = Math.Min(first + RowsPerPage, _entries.Length);
        for (var index = first; index < last; index++)
        {
            var rowIndex = index;
            var row = _entries[index];
            var skipped = _skipped.Contains(row.Entry);
            var state = skipped ? "Skipped"
                      : row.Target is null ? "Match required"
                      : row.Existing?.IsRemoved == true ? "Previously removed"
                      : !row.HasChanges() ? "Unchanged"
                      : !Ready(row) ? "Conflict review required"
                      : row.Existing is null ? "Ready to add"
                      : "Ready to enrich";
            var creator = row.Entry.Metadata.GetValueOrDefault(RecommendationField.Recommender);
            _rows.Children.Add(new Label { Text = $"{row.Entry.Title} · {creator}", FontAttributes = FontAttributes.Bold });
            _rows.Children.Add(new Label { Text = $"{row.Entry.YearAndType}\n{state}" });
            if (row.Target is not null)
                _rows.Children.Add(new Label { Text = $"Saved item: {row.Target.ImportReviewDisplay}" });
            if (row.Entry.ReviewNote.Length > 0)
                _rows.Children.Add(new Label { Text = "Source discrepancy flagged. Review the incoming details before matching." });
            _rows.Children.Add(ActionButton("Incoming details", () => ShowIncomingAsync(row.Entry)));
            _rows.Children.Add(ActionButton(row.Target is null ? "Choose saved item" : "Change match", () => MatchAsync(rowIndex)));
            if (row.HasChanges() && row.Conflicts().Count > 0)
                _rows.Children.Add(ActionButton("Review conflicting fields", () => ReviewConflictsAsync(row)));
            if (row.HasChanges())
                _rows.Children.Add(ActionButton(skipped ? "Include entry" : "Skip entry", () =>
                {
                    if (skipped) _skipped.Remove(row.Entry);
                    else _skipped.Add(row.Entry);
                    Render();
                    return Task.CompletedTask;
                }));
        }
        var ready = _entries.Where(Ready).ToArray();
        _summary.Text = _entries.Length == 0 ? "Choose a file to start." :
            $"New {ready.Count(row => row.Existing is null)} · Enrich {ready.Count(row => row.Existing is not null)}" +
            $" · Unchanged or removed {_entries.Count(row => row.Target is not null && !row.HasChanges())}\n" +
            $"Conflict review {_entries.Count(NeedsConflictReview)} · Unmatched {_entries.Count(row => row.Target is null)} · Skipped {_skipped.Count}";
        _range.Text = _entries.Length == 0 ? string.Empty : $"Rows {first + 1}–{last} of {_entries.Length}";
        _previous.IsVisible = _page > 0;
        _next.IsVisible     = last < _entries.Length;
        _apply.IsVisible    = ready.Length > 0;
    }

    private async Task MoveAsync(int direction)
    {
        _page += direction;
        Render();
        await _scroll.ScrollToAsync(0, 0, false);
    }

    private Button ActionButton(string label, Func<Task> action)
    {
        var button = new Button { Text = label };
        button.SetDynamicResource(StyleProperty, "SecondaryButtonStyle");
        button.Clicked += async (_, _) =>
        {
            if (_busy) return;
            _busy = true;
            button.IsEnabled = false;
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                await DisplayAlertAsync("Enhanced import stopped", exception.Message, "OK");
            }
            finally
            {
                _busy = false;
                button.IsEnabled = true;
            }
        };
        return button;
    }
}
