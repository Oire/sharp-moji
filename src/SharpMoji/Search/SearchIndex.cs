using System.Collections.Immutable;

namespace Oire.SharpMoji;

/// <summary>
/// The folded text a catalog searches over, built once when the catalog is loaded.
/// </summary>
/// <remarks>
/// A linear scan over 1949 emoji is fast enough that a trie or inverted index would be effort
/// spent on the wrong problem — the dataset is small and the expensive part, folding, is done up
/// front. What matters for a picker is that results are <em>right</em>, not that the scan is
/// sub-microsecond.
/// </remarks>
internal sealed class SearchIndex {
    private readonly ImmutableArray<Entry> _entries;

    public SearchIndex(ImmutableArray<Emoji> emoji) {
        var entries = ImmutableArray.CreateBuilder<Entry>(emoji.Length);

        foreach (var item in emoji) {
            entries.Add(new Entry(
                item,
                TextFolding.Fold(item.Label),
                [.. item.Tags.Select(TextFolding.Fold)]));
        }

        _entries = entries.ToImmutable();
    }

    /// <summary>Ranks every emoji whose text matches, best first.</summary>
    public ImmutableArray<EmojiMatch> Search(string query, int limit) {
        var folded = TextFolding.Fold(query.Trim());

        if (folded.Length == 0 || limit <= 0) {
            return [];
        }

        var terms = TextFolding.SplitTerms(folded);

        if (terms.Length == 0) {
            return [];
        }

        var results = new List<EmojiMatch>();

        foreach (var entry in _entries) {
            if (Classify(entry, folded, terms) is { } match) {
                results.Add(match);
            }
        }

        results.Sort(static (left, right) => {
            var byKind = left.Kind.CompareTo(right.Kind);

            if (byKind != 0) {
                return byKind;
            }

            // Then the shorter name. Canonical order is not popularity order, and within one match
            // kind it is effectively arbitrary to a user: everything tagged "love" is equally
            // tagged, but Unicode happens to sort ❤️ after 💌 💘 💝 💖, so ranking by order alone
            // pushes the heart people actually mean onto the second screen. A shorter name is a
            // decent proxy for a more basic emoji — "red heart" against "heart with ribbon".
            //
            // This is a heuristic, not a truth, which is why the golden corpus exists to check it.
            var byLength = left.Emoji.Label.Length.CompareTo(right.Emoji.Label.Length);

            if (byLength != 0) {
                return byLength;
            }

            // Canonical order last, so the same query always returns the same list. Without a total
            // order, results would shuffle between runs and read as a broken UI.
            return (left.Emoji.Order ?? int.MaxValue).CompareTo(right.Emoji.Order ?? int.MaxValue);
        });

        return [.. results.Take(limit)];
    }

    /// <summary>
    /// Decides how one emoji matched, or that it did not.
    /// </summary>
    /// <remarks>
    /// The whole query is tried first, so <c>"grinning face"</c> is treated as a name rather than
    /// as two loose words. Only if that fails does it fall back to requiring every term to appear
    /// somewhere, which is what someone typing <c>flag turkey</c> means.
    /// </remarks>
    private static EmojiMatch? Classify(Entry entry, string query, string[] terms) {
        if (entry.Label == query) {
            return entry.Match(MatchKind.Label, entry.Emoji.Label);
        }

        // Exact tag before label prefix: see the remarks on MatchKind.Tag for why this order.
        for (var i = 0; i < entry.Tags.Length; i++) {
            if (entry.Tags[i] == query) {
                return entry.Match(MatchKind.Tag, entry.Emoji.Tags[i]);
            }
        }

        if (entry.Label.StartsWith(query, StringComparison.Ordinal)) {
            return entry.Match(MatchKind.LabelPrefix, entry.Emoji.Label);
        }

        if (entry.Label.Contains(query, StringComparison.Ordinal)) {
            return entry.Match(MatchKind.LabelSubstring, entry.Emoji.Label);
        }

        for (var i = 0; i < entry.Tags.Length; i++) {
            if (entry.Tags[i].Contains(query, StringComparison.Ordinal)) {
                return entry.Match(MatchKind.TagSubstring, entry.Emoji.Tags[i]);
            }
        }

        // Multi-term fallback: every term must appear somewhere in the label or the tags.
        return terms.Length > 1 && terms.All(term => entry.ContainsTerm(term))
            ? entry.Match(MatchKind.TagSubstring, entry.Emoji.Label)
            : null;
    }

    private readonly record struct Entry(Emoji Emoji, string Label, ImmutableArray<string> Tags) {
        public EmojiMatch Match(MatchKind kind, string matchedText) =>
            new() { Emoji = Emoji, Kind = kind, MatchedText = matchedText };

        public bool ContainsTerm(string term) {
            if (Label.Contains(term, StringComparison.Ordinal)) {
                return true;
            }

            foreach (var tag in Tags) {
                if (tag.Contains(term, StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }
    }
}
