namespace Oire.SharpMoji;

/// <summary>
/// Normalizes emoji sequences so that equivalent forms compare equal.
/// </summary>
/// <remarks>
/// <para>
/// The problem this exists to solve: 517 of 1949 records store a fully-qualified sequence ending
/// in U+FE0F (VARIATION SELECTOR-16), while the same emoji typed, pasted or copied out of another
/// application usually arrives without it. Thumbs up is stored as
/// <c>[U+1F44D, U+FE0F]</c>; a user hands you <c>[U+1F44D]</c>. A dictionary keyed on the stored
/// form misses every one of them, silently.
/// </para>
/// <para>
/// Both sides are therefore normalized by removing every U+FE0F. That also makes the
/// minimally-qualified and unqualified forms in Unicode's own <c>emoji-test.txt</c> resolve to the
/// same emoji as their fully-qualified counterparts, which is how the conformance tests check it.
/// </para>
/// </remarks>
internal static class EmojiSequences {
    private const char VariationSelector16 = '️';

    /// <summary>
    /// Returns the lookup key for a sequence: the same text with every U+FE0F removed.
    /// </summary>
    /// <remarks>
    /// Returns the original string when there is nothing to strip, so the common case allocates
    /// nothing at all.
    /// </remarks>
    public static string Normalize(string sequence) {
        var index = sequence.IndexOf(VariationSelector16, StringComparison.Ordinal);

        if (index < 0) {
            return sequence;
        }

        return string.Create(sequence.Length - CountSelectors(sequence, index), (sequence, index), static (span, state) => {
            var (source, firstSelector) = state;

            source.AsSpan(0, firstSelector).CopyTo(span);

            var written = firstSelector;

            for (var i = firstSelector; i < source.Length; i++) {
                if (source[i] != VariationSelector16) {
                    span[written++] = source[i];
                }
            }
        });
    }

    private static int CountSelectors(string sequence, int from) {
        var count = 0;

        for (var i = from; i < sequence.Length; i++) {
            if (sequence[i] == VariationSelector16) {
                count++;
            }
        }

        return count;
    }
}
