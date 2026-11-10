using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class EnhancedRecommendationParserTests
{
    [Fact]
    public void Parse_DistinctVideos_PreservesEvidenceAndNegativeVerdict()
    {
        var markdown = """
            ## Video 1 — @one
            - ⚠ Source details disagree.
            ### Coherence — Video 1
            - **Title:** Coherence
            - **Year / Type:** 2013 (inventory) — film
            - **Source:** @one — https://www.instagram.com/reel/One/
            - **Title-specific rationale:** The creator gives a negative verdict: skip it.
            - **Confidence:** High — direct caption
            - **Evidence:** Caption (direct).
            - **Contains spoilers:** true
            ## Video 2 — @one
            ### Coherence — Video 2
            - **Title:** Coherence
            - **Source:** @one — https://www.instagram.com/reel/Two/
            - **Title-specific rationale:** Not provided by creator — no rationale.
            - **Streaming (as described in video):** Netflix
            ## Video 3 — @one
            - **Caption (direct):** A tutorial.
            ## Reconciliation summary
            """;

        var entries = new EnhancedRecommendationParser().Parse(markdown);

        Assert.Equal(2, entries.Count);
        Assert.NotEqual(entries[0].SourceKey, entries[1].SourceKey);
        Assert.Equal("Negative", entries[0].Metadata[RecommendationField.Sentiment]);
        Assert.Equal("true", entries[0].Metadata[RecommendationField.ContainsSpoilers]);
        Assert.Contains("Source details disagree", entries[0].Metadata[RecommendationField.Evidence]);
        Assert.False(entries[1].Metadata.ContainsKey(RecommendationField.Rationale));
        Assert.Equal("Netflix", entries[1].Metadata[RecommendationField.HistoricalStreaming]);
    }

    [Fact]
    public void Parse_DuplicateOccurrence_RejectsAmbiguousFile()
    {
        var markdown = """
            ## Video 1 — @one
            ### Coherence — Video 1
            - **Title:** Coherence
            - **Source:** @one — https://www.instagram.com/reel/One/
            ### Coherence — Video 1
            - **Title:** Coherence
            - **Source:** @one — https://instagram.com/reel/One?igsh=other
            """;

        var error = Assert.Throws<InvalidDataException>(() => new EnhancedRecommendationParser().Parse(markdown));

        Assert.Contains("repeats a title", error.Message);
    }
}
