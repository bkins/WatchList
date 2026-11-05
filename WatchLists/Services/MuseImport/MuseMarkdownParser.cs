using System.Text.RegularExpressions;

namespace WatchLists.Services.MuseImport;

public sealed class MuseMarkdownParser
{
    private static readonly Regex ItemPattern = new(@"^\s*[-*] \*\*(?<title>.+?)\*\*(?:\s*\((?<year>\d{4})\))?\s*[—–-]\s*(?<description>.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex LinkPattern = new(@"\]\((?<url>https?://[^\s)]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public MuseParseResult Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var recommendations = new List<MuseRecommendation>();
        var warnings        = new List<string>();
        var lines           = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var section         = string.Empty;
        var index           = 0;

        while (index < lines.Length)
        {
            var line = lines[index];
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                section = line[3..].Trim();
            }
            else if (line.TrimStart().StartsWith("- **", StringComparison.Ordinal))
            {
                var match = ItemPattern.Match(line);
                if (!match.Success)
                {
                    warnings.Add($"Line {index + 1}: Unrecognized recommendation format.");
                    index++;
                    continue;
                }

                var urls = new List<string>();
                var next = index + 1;
                while (next < lines.Length && !string.IsNullOrWhiteSpace(lines[next]) &&
                       !lines[next].TrimStart().StartsWith("- **", StringComparison.Ordinal) &&
                       !lines[next].StartsWith("## ", StringComparison.Ordinal))
                {
                    if (lines[next].TrimStart().StartsWith("Source:", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (Match link in LinkPattern.Matches(lines[next]))
                        {
                            var url = link.Groups["url"].Value;
                            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                                !urls.Contains(url, StringComparer.OrdinalIgnoreCase))
                            {
                                urls.Add(url);
                            }
                        }
                    }
                    next++;
                }

                if (urls.Count == 0)
                {
                    warnings.Add($"Line {index + 1}: No source URL found.");
                }

                var year = int.TryParse(match.Groups["year"].Value, out var parsedYear) ? parsedYear : (int?)null;
                recommendations.Add(new MuseRecommendation(match.Groups["title"].Value.Trim()
                                                         , year
                                                         , match.Groups["description"].Value.Trim()
                                                         , section
                                                         , urls
                                                         , index + 1));
                index = next;
                continue;
            }
            index++;
        }

        if (recommendations.Count == 0)
        {
            warnings.Add("No recommendations found; check the document format.");
        }

        return new MuseParseResult(recommendations, warnings);
    }
}
