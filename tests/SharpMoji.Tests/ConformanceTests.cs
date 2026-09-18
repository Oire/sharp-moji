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
/// Two files are needed, because neither alone can check both directions. Unicode publishes these
/// under <c>/Public/emoji/&lt;version&gt;/</c>, which stops at 16.0, while <c>/latest/</c> is
/// already 18.0 — there is no 17.0 file matching the shipped data. So 16.0 is used as a subset
/// (everything it lists must resolve) and 18.0 as a superset (everything shipped must be listed).
/// </para>
/// <para>
/// Using the subset file for the superset direction is a trap worth naming: every emoji introduced
/// after 16.0 then looks like something Unicode does not define. An earlier version of these tests
/// did exactly that and concluded 150 skin-tone variants were outside Unicode's recommended set.
/// They are not — they were simply added after 16.0.
/// </para>
/// </remarks>
[Trait("Category", "Conformance")]
public class ConformanceTests {
    // ---------------------------------------------------------------------------------------
    // Subset direction: everything the older list names must resolve.
    // ---------------------------------------------------------------------------------------

    [SkippableFact]
    public void EveryFullyQualifiedSequence_ResolvesThroughTheCatalog() {
        Skip.IfNot(EmojiTestFile.IsAvailable, EmojiTestFile.SkipReason);

        var catalog = EmojiCatalog.English;
        var missing = EmojiTestFile.Read(EmojiTestFile.SubsetVersion)
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
        var partial = EmojiTestFile.Read(EmojiTestFile.SubsetVersion)
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
        var byHexcode = EmojiTestFile.Read(EmojiTestFile.SubsetVersion)
            .Where(e => e.Status != EmojiQualification.Component)
            .GroupBy(e => Unqualify(e.Hexcode));

        var divergent = new List<string>();

        foreach (var group in byHexcode.Where(g => g.Count() > 1)) {
            var resolved = group.Select(e => catalog.Find(e.Sequence)?.Hexcode).Distinct().ToList();

            if (resolved.Count > 1) {
                divergent.Add($"{group.Key}: {string.Join(" / ", resolved)}");
            }
        }

        divergent.Take(10).Should().BeEmpty("all spellings of one emoji must resolve to one record");
    }

    // ---------------------------------------------------------------------------------------
    // Superset direction: everything shipped must be something Unicode defines.
    // ---------------------------------------------------------------------------------------

    [SkippableFact]
    public void TheOnlyBaseEmoji_UnicodeDoesNotListAreRegionalIndicators() {
        Skip.IfNot(EmojiTestFile.IsAvailable, EmojiTestFile.SkipReason);

        // This does not come out empty, and the exception is legitimate: Unicode lists regional
        // indicators only in pairs, as flags, never standalone. Emojibase carries the 26 letters
        // individually because they are the building blocks flags are made of.
        //
        // Pinned rather than merely allowed, so anything else appearing here fails.
        var known = KnownSequences();
        var unlisted = EmojiCatalog.English.AllIncludingComponents
            .Where(e => !known.Contains(Unqualify(e.Hexcode)))
            .ToList();

        unlisted.Should().HaveCount(26);
        unlisted.Should().OnlyContain(e => e.Label.StartsWith("regional indicator", StringComparison.Ordinal));

        // These are exactly the records that carry no group, which is why SPEC 3.2 requires the
        // group property to be nullable.
        unlisted.Should().OnlyContain(e => e.Group == null);
    }

    [SkippableFact]
    public void EverySkinToneVariant_IsASequenceUnicodeDefines() {
        Skip.IfNot(EmojiTestFile.IsAvailable, EmojiTestFile.SkipReason);

        // Every single one, with no exceptions. This is the test that would break first if anything
        // ever composed tone sequences by inserting modifiers instead of using the published
        // variants - which is precisely what SPEC 5.3 forbids.
        var known = KnownSequences();
        var unknown = EmojiCatalog.English.All
            .SelectMany(e => e.Skins)
            .Where(s => !known.Contains(Unqualify(s.Hexcode)))
            .Select(s => $"{s.Sequence} {s.Hexcode} {s.Label}")
            .Take(20)
            .ToList();

        unknown.Should().BeEmpty("all 2030 skin-tone sequences are recommended by Unicode");
    }

    [SkippableFact]
    public void EmojiNewerThanTheSubsetFile_AreStillKnownToUnicode() {
        Skip.IfNot(EmojiTestFile.IsAvailable, EmojiTestFile.SkipReason);

        // Guards the specific mistake this suite previously made. The eight emoji introduced after
        // 16.0 are absent from the subset file but present in the superset one, and checking them
        // against the wrong file is what manufactured a finding that did not exist.
        var known = KnownSequences();
        var newer = EmojiCatalog.English.All.Where(e => e.UnicodeVersion > 16.0).ToList();

        newer.Should().NotBeEmpty("the shipped data is newer than the subset conformance file");
        newer.Should().OnlyContain(e => known.Contains(Unqualify(e.Hexcode)));
    }

    private static HashSet<string> KnownSequences() =>
        EmojiTestFile.Read(EmojiTestFile.SupersetVersion)
            .Select(e => Unqualify(e.Hexcode))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Strips variation selectors, so the two spellings of a sequence compare equal.</summary>
    private static string Unqualify(string hexcode) =>
        string.Join('-', hexcode.Split('-').Where(p => !string.Equals(p, "FE0F", StringComparison.OrdinalIgnoreCase)));
}
