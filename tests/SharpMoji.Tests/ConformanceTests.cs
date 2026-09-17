using FluentAssertions;
using Oire.SharpMoji.DataTool.Emojibase;
using Xunit;

namespace Oire.SharpMoji.Tests;

/// <summary>
/// Phase 2 exit criterion: the catalog agrees with Unicode's own list of emoji.
/// </summary>
/// <remarks>
/// <para>
/// Checking SharpMoji against the Emojibase corpus it was generated from would be circular. These
/// tests use <c>emoji-test.txt</c>, published by Unicode, as an independent source.
/// </para>
/// <para>
/// It is pinned to emoji 16.0 because there is no 17.0 file to match the data: Unicode publishes
/// these under <c>/Public/emoji/&lt;version&gt;/</c>, which stops at 16.0, while <c>/latest/</c>
/// is already 18.0. Since emoji are never removed from Unicode, 16.0 is a strict subset of the
/// 17.0 data, and the tests below assert that relationship in both directions rather than
/// pretending to an exact match.
/// </para>
/// </remarks>
[Trait("Category", "Conformance")]
public class ConformanceTests {
    [SkippableFact]
    public void EveryFullyQualifiedSequence_ResolvesThroughTheCatalog() {
        Skip.IfNot(EmojiTestFile.IsAvailable, EmojiTestFile.SkipReason);

        var catalog = EmojiCatalog.English;
        var missing = EmojiTestFile.Read()
            .Where(e => e.Status == EmojiQualification.FullyQualified)
            .Where(e => catalog.Find(e.Sequence) is null)
            .Select(e => $"{e.Sequence} {e.Hexcode} {e.Name}")
            .Take(20)
            .ToList();

        missing.Should().BeEmpty("every emoji Unicode lists must be findable");
    }

    [SkippableFact]
    public void MinimallyQualifiedAndUnqualifiedForms_ResolveToTheSameEmoji() {
        Skip.IfNot(EmojiTestFile.IsAvailable, EmojiTestFile.SkipReason);

        // This is the test that matters most in practice. These entries are the same emoji written
        // with some U+FE0F omitted - which is the form a pasted or typed emoji usually arrives in.
        // A catalog keyed on the stored fully-qualified sequence would miss every one of them,
        // silently returning "no such emoji" for input the user can plainly see is an emoji.
        var catalog = EmojiCatalog.English;
        var partial = EmojiTestFile.Read()
            .Where(e => e.Status is EmojiQualification.MinimallyQualified or EmojiQualification.Unqualified)
            .ToList();

        partial.Should().NotBeEmpty("the fixture should contain partially-qualified entries");

        var unresolved = partial
            .Where(e => catalog.Find(e.Sequence) is null)
            .Select(e => $"{e.Sequence} {e.Hexcode} {e.Name}")
            .Take(20)
            .ToList();

        unresolved.Should().BeEmpty("normalizing U+FE0F away is what makes pasted emoji work (SPEC 3.6)");
    }

    [SkippableFact]
    public void QualifiedAndUnqualifiedForms_AreTheSameRecord() {
        Skip.IfNot(EmojiTestFile.IsAvailable, EmojiTestFile.SkipReason);

        var catalog = EmojiCatalog.English;

        // Resolving is not enough: both spellings must land on one record, or a picker would show
        // the same emoji twice and favorites saved under one spelling would not match the other.
        var byHexcode = EmojiTestFile.Read()
            .Where(e => e.Status != EmojiQualification.Component)
            .GroupBy(e => e.Hexcode.Replace("-FE0F", string.Empty, StringComparison.Ordinal));

        var divergent = new List<string>();

        foreach (var group in byHexcode.Where(g => g.Count() > 1)) {
            var resolved = group.Select(e => catalog.Find(e.Sequence)?.Hexcode).Distinct().ToList();

            if (resolved.Count > 1) {
                divergent.Add($"{group.Key}: {string.Join(" / ", resolved)}");
            }
        }

        divergent.Take(10).Should().BeEmpty("all spellings of one emoji must resolve to one record");
    }

    [SkippableFact]
    public void TheOnlyBaseEmoji_UnicodeDoesNotListAreRegionalIndicators() {
        Skip.IfNot(EmojiTestFile.IsAvailable, EmojiTestFile.SkipReason);

        // The converse direction, which catches data the generator invented or mangled. It does not
        // come out empty, and the exception is legitimate: Unicode's list contains regional
        // indicators only in pairs, as flags, never standalone. Emojibase carries the 26 letters
        // individually because they are the building blocks flags are made of.
        //
        // Pinned rather than merely allowed, so that anything else appearing here fails.
        var unlisted = Unlisted(EmojiCatalog.English.AllIncludingComponents.Where(e => e.UnicodeVersion <= 16.0));

        unlisted.Should().HaveCount(26);
        unlisted.Should().OnlyContain(e => e.Label.StartsWith("regional indicator", StringComparison.Ordinal));

        // These are exactly the records that carry no group, which is why SPEC 3.2 requires the
        // group property to be nullable.
        unlisted.Should().OnlyContain(e => e.Group == null);
    }

    [SkippableFact]
    public void SomeSkinToneVariants_AreValidButNotRecommendedByUnicode() {
        Skip.IfNot(EmojiTestFile.IsAvailable, EmojiTestFile.SkipReason);

        // Emojibase supplies skin-tone sequences for six emoji that Unicode does not recommend for
        // general interchange. The sequences are well-formed, but a platform is under no obligation
        // to render them as one glyph, so they may appear as the base emoji followed by a stray
        // tone swatch.
        //
        // This matters for a picker: offering a tone that renders broken is worse than not
        // offering it. Phase 3 should expose the distinction rather than hide it - see SPEC 5.3.
        var catalog = EmojiCatalog.English;
        var known = KnownHexcodes();

        var affected = catalog.All
            .Where(e => e.UnicodeVersion <= 16.0)
            .Where(e => e.Skins.Any(s => !known.Contains(Unqualify(s.Hexcode))))
            .ToList();

        affected.Should().HaveCount(6, "the exception is narrow and should stay narrow");
        affected.Should().OnlyContain(e =>
            e.Label.Contains("bunny ears", StringComparison.Ordinal)
            || e.Label.Contains("wrestling", StringComparison.Ordinal));

        // Every affected emoji is affected wholly: all 25 of its variants are outside the
        // recommended set, not some awkward subset.
        affected.Should().OnlyContain(e => e.Skins.All(s => !known.Contains(Unqualify(s.Hexcode))));

        var total = catalog.All.Where(e => e.UnicodeVersion <= 16.0).SelectMany(e => e.Skins)
            .Count(s => !known.Contains(Unqualify(s.Hexcode)));

        total.Should().Be(150, "6 emoji x 25 variants");
    }

    [SkippableFact]
    public void EveryOtherSkinToneVariant_IsASequenceUnicodeDefines() {
        Skip.IfNot(EmojiTestFile.IsAvailable, EmojiTestFile.SkipReason);

        // With the six known exceptions set aside, every skin-tone sequence must be one Unicode
        // actually defines. This is the part that would break first if anything ever composed tone
        // sequences by inserting modifiers instead of using the published variants.
        var known = KnownHexcodes();

        var unknown = EmojiCatalog.English.All
            .Where(e => e.UnicodeVersion <= 16.0)
            .Where(e => !e.Label.Contains("bunny ears", StringComparison.Ordinal)
                && !e.Label.Contains("wrestling", StringComparison.Ordinal))
            .SelectMany(e => e.Skins)
            .Where(s => !known.Contains(Unqualify(s.Hexcode)))
            .Select(s => $"{s.Sequence} {s.Hexcode} {s.Label}")
            .Take(20)
            .ToList();

        unknown.Should().BeEmpty();
    }

    private static List<Emoji> Unlisted(IEnumerable<Emoji> emoji) {
        var known = KnownHexcodes();

        return [.. emoji.Where(e => !known.Contains(Unqualify(e.Hexcode)))];
    }

    private static HashSet<string> KnownHexcodes() =>
        EmojiTestFile.Read().Select(e => Unqualify(e.Hexcode)).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Strips variation selectors, so the two spellings of a sequence compare equal.</summary>
    private static string Unqualify(string hexcode) =>
        string.Join('-', hexcode.Split('-').Where(p => !string.Equals(p, "FE0F", StringComparison.OrdinalIgnoreCase)));
}
