using System.Text.RegularExpressions;

namespace WatchLists.Services.MuseImport;

public sealed class EnhancedRecommendationParser
{
    private static readonly Regex VideoHeading = new(@"^## Video (?<number>\d+) — ", RegexOptions.CultureInvariant);
    private static readonly Regex EntryHeading = new(@"^### (?<title>.+) — Video (?<number>\d+)$", RegexOptions.CultureInvariant);
    private static readonly Regex FieldLine    = new(@"^- \*\*(?<name>[^*]+):\*\*\s*(?<value>.*)$", RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, RecommendationField> Fields = new(StringComparer.Ordinal)
    {
        ["Recommendation context"]                        = RecommendationField.Context
      , ["Title-specific rationale"]                       = RecommendationField.Rationale
      , ["Creator reaction / superlatives / commentary"]   = RecommendationField.Commentary
      , ["Notable characteristics (creator-emphasized)"]   = RecommendationField.Characteristics
      , ["Comparisons"]                                   = RecommendationField.Comparisons
      , ["Viewing instructions / spoiler warnings"]        = RecommendationField.ViewingInstructions
      , ["Rank / Score"]                                  = RecommendationField.Rank
      , ["Streaming (as described in video)"]              = RecommendationField.HistoricalStreaming
      , ["Evidence"]                                      = RecommendationField.Evidence
      , ["Confidence"]                                    = RecommendationField.Confidence
      , ["Sentiment"]                                     = RecommendationField.Sentiment
      , ["Contains spoilers"]                             = RecommendationField.ContainsSpoilers
    };

    public IReadOnlyList<EnhancedRecommendation> Parse(string markdown)
    {
        using var reader = new StringReader(markdown);
        var results = new List<EnhancedRecommendation>();
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var notes = new List<string>();
        var video = string.Empty;
        string? title = null;

        while (reader.ReadLine() is { } line)
        {
            line = line.Trim().TrimStart('\uFEFF');
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                FinishEntry();
                video = VideoHeading.Match(line).Groups["number"].Value;
                notes.Clear();
            }
            else if (video.Length > 0 && line.StartsWith("### ", StringComparison.Ordinal))
            {
                FinishEntry();
                var entry = EntryHeading.Match(line);
                if (!entry.Success || entry.Groups["number"].Value != video)
                    throw new InvalidDataException($"Invalid recommendation heading in video {video}.");
                title = entry.Groups["title"].Value;
            }
            else if (video.Length > 0 && line.StartsWith("- ⚠", StringComparison.Ordinal))
            {
                notes.Add(line[2..]);
            }
            else if (title is not null && FieldLine.Match(line) is { Success: true } field
                     && !fields.TryAdd(field.Groups["name"].Value, field.Groups["value"].Value.Trim()))
            {
                throw new InvalidDataException($"Duplicate field for {title}.");
            }
        }
        FinishEntry();

        if (results.Count == 0)
            throw new InvalidDataException("No enhanced entries found. Choose the complete Muse enhanced analysis Markdown file.");
        if (results.GroupBy(entry => (entry.Title.ToUpperInvariant(), entry.SourceKey)).Any(group => group.Count() > 1))
            throw new InvalidDataException("The file repeats a title and source occurrence. Review the duplicate in the source file.");
        return results;

        void FinishEntry()
        {
            if (title is null) return;
            results.Add(BuildEntry(title, fields, string.Join("\n", notes)));
            fields.Clear();
            title = null;
        }
    }

    private static EnhancedRecommendation BuildEntry(string title, Dictionary<string, string> fields, string note)
    {
        var source = fields.GetValueOrDefault("Source")?.Split(" — ", 2, StringSplitOptions.TrimEntries);
        if (source is not { Length: 2 } || fields.GetValueOrDefault("Title") != title)
            throw new InvalidDataException($"The title or source is missing or inconsistent for {title}.");

        var metadata = new Dictionary<RecommendationField, string>
                       {
                           [RecommendationField.Recommender] = source[0]
                         , [RecommendationField.SourceType]  = "Instagram"
                       };
        foreach (var (label, value) in fields)
        {
            if (Fields.TryGetValue(label, out var field) && HasContent(value))
                metadata.Add(field, value);
        }

        var rationale = metadata.GetValueOrDefault(RecommendationField.Rationale) ?? string.Empty;
        if (!metadata.ContainsKey(RecommendationField.Sentiment))
        {
            var verdict = metadata.GetValueOrDefault(RecommendationField.Rank);
            if (verdict == "Creator verdict: no" || rationale.Contains("a negative verdict:", StringComparison.OrdinalIgnoreCase))
                metadata.Add(RecommendationField.Sentiment, "Negative");
            else if (rationale.Contains("judgement is mixed", StringComparison.OrdinalIgnoreCase))
                metadata.Add(RecommendationField.Sentiment, "Mixed");
            else if (verdict == "Creator verdict: yes")
                metadata.Add(RecommendationField.Sentiment, "Positive");
        }
        if (!metadata.ContainsKey(RecommendationField.ContainsSpoilers)
            && (metadata.GetValueOrDefault(RecommendationField.ViewingInstructions)?.Contains("contains spoilers", StringComparison.OrdinalIgnoreCase) ?? false))
            metadata.Add(RecommendationField.ContainsSpoilers, "true");

        var yearAndType = fields.GetValueOrDefault("Year / Type") ?? string.Empty;
        if (yearAndType.Length > 0 || note.Length > 0)
            metadata[RecommendationField.Evidence] = string.Join("\n", new[]
            {
                metadata.GetValueOrDefault(RecommendationField.Evidence), yearAndType, note
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        _ = RecommendationMetadata.Write(metadata);
        return new EnhancedRecommendation(title, yearAndType, source[1], RecommendationSourceKey.Create(source[1]), metadata, note);
    }

    private static bool HasContent(string value)
    {
        return value.Length > 0
            && !value.StartsWith("Not provided by creator", StringComparison.OrdinalIgnoreCase)
            && value is not ("None reported" or "None stated" or "Not stated" or "Unavailable");
    }
}
