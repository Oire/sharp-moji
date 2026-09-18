using System.Globalization;
using System.Text;

namespace Oire.SharpMoji;

/// <summary>
/// Reduces text to the form used for matching: lowercase, without diacritics.
/// </summary>
/// <remarks>
/// <para>
/// Folding happens once per label and tag when a catalog is built, and once per query. Comparing
/// with <see cref="CompareOptions.IgnoreNonSpace"/> on every candidate instead would mean roughly
/// fifteen thousand culture-aware comparisons per keystroke.
/// </para>
/// <para>
/// Case is folded with the <em>invariant</em> culture, deliberately. The ambient culture must not
/// decide: a user whose machine is set to Turkish would otherwise get different results from an
/// English catalog, because Turkish maps <c>I</c> to <c>ı</c> rather than <c>i</c>. None of the
/// embedded languages need locale-specific casing, and invariant folding also keeps the library
/// working under <c>InvariantGlobalization</c>.
/// </para>
/// <para>
/// Diacritics are stripped so that someone who cannot easily type them still finds what they
/// mean — <c>cafe</c> finds <c>café</c>. This does treat <c>ö</c> as a form of <c>o</c>, which in
/// Swedish is a distinct letter rather than an accent; for search that is the forgiving choice,
/// and exact matches still outrank the folded ones (see <see cref="MatchKind"/>).
/// </para>
/// </remarks>
internal static class TextFolding {
    /// <summary>Folds text for matching.</summary>
    public static string Fold(string text) {
        if (text.Length == 0) {
            return text;
        }

        var lowered = text.ToLowerInvariant();

        // Decomposing separates a base letter from its accents, which then show up as non-spacing
        // marks and can simply be dropped. Text with nothing to decompose is returned untouched,
        // which covers most queries and allocates nothing further.
        if (lowered.IsNormalized(NormalizationForm.FormD)) {
            return lowered;
        }

        var decomposed = lowered.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed) {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Splits a query into the terms that must all match.</summary>
    /// <remarks>
    /// Multi-word queries are treated as "all of these", because that is what someone typing
    /// <c>flag turkey</c> means. Matching any one term instead would bury the intended result
    /// under everything that merely mentions a flag.
    /// </remarks>
    public static string[] SplitTerms(string foldedQuery) =>
        foldedQuery.Split(
            [' ', '\t', '\n', '_', '-', ':'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
