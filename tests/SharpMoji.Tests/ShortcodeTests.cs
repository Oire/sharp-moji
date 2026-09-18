using FluentAssertions;
using Xunit;

namespace Oire.SharpMoji.Tests;

/// <summary>
/// Phase 4: shortcode lookup.
/// </summary>
/// <remarks>
/// Shortcodes are a separate data axis, not a field on the emoji record — the February 2026 draft
/// had them as <c>Emoji.Shortcodes</c>, which does not match the source at all (SPEC 3.4).
/// </remarks>
public class ShortcodeTests {
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1859:Use concrete types when possible",
        Justification = "Testing through the public interface is the intent.")]
    private static readonly IEmojiCatalog Catalog = EmojiCatalog.English;

    private const string ThumbsUp = "\U0001F44D";

    [Fact]
    public void FindByShortcode_ResolvesTheCldrName() =>
        Catalog.FindByShortcode("thumbs_up")!.Hexcode.Should().Be("1F44D");

    [Fact]
    public void FindByShortcode_ResolvesConventionsThatExistInNoLabelOrTag() {
        // The reason shortcodes are worth shipping at all. None of these appear in any label or
        // tag, so search could never find them - they are pure convention.
        Catalog.FindByShortcode("+1")!.Hexcode.Should().Be("1F44D");
        Catalog.FindByShortcode("-1")!.Hexcode.Should().Be("1F44E");
        Catalog.FindByShortcode("tm")!.Hexcode.Should().Be("2122");

        // GitHub's own inventions like :shipit: are custom images rather than Unicode emoji, so
        // they are legitimately absent - this data describes Unicode, not any one site's sticker set.
        Catalog.FindByShortcode("shipit").Should().BeNull();
    }

    [Theory]
    [InlineData("+1")]
    [InlineData(":+1:")]
    [InlineData("  :+1:  ")]
    [InlineData(":+1")]
    public void FindByShortcode_AcceptsTheColonsApplicationsLeaveAttached(string shortcode) {
        // Text being scanned for emoji arrives as ":+1:", not "+1". Making callers trim first
        // would make the common case the awkward one.
        Catalog.FindByShortcode(shortcode)!.Hexcode.Should().Be("1F44D");
    }

    [Fact]
    public void FindByShortcode_IsCaseInsensitive() =>
        Catalog.FindByShortcode("THUMBS_UP")!.Hexcode.Should().Be("1F44D");

    [Fact]
    public void FindByShortcode_ReturnsNullRatherThanThrowing_ForNonsense() {
        Catalog.FindByShortcode("definitely_not_a_shortcode").Should().BeNull();
        Catalog.FindByShortcode(":::").Should().BeNull();
        Catalog.FindByShortcode(string.Empty).Should().BeNull();
        Catalog.FindByShortcode(null).Should().BeNull();
    }

    // ---------------------------------------------------------------------------------------
    // Presets.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Presets_DisagreeWithEachOther_WhichIsWhyTheyAreSelectable() {
        // GitHub calls it "+1"; CLDR calls it "thumbs_up". An application matching one ecosystem's
        // text needs to say which.
        Catalog.FindByShortcode("+1", ShortcodePreset.GitHub)!.Hexcode.Should().Be("1F44D");
        Catalog.FindByShortcode("+1", ShortcodePreset.Cldr).Should().BeNull();

        Catalog.FindByShortcode("thumbs_up", ShortcodePreset.Cldr)!.Hexcode.Should().Be("1F44D");
    }

    [Fact]
    public void GetShortcodes_ReturnsWhatAPresetCallsAnEmoji() {
        Catalog.GetShortcodes(ThumbsUp).Should().Contain("thumbs_up");
        Catalog.GetShortcodes(ThumbsUp, ShortcodePreset.GitHub).Should().Contain("+1");

        // Emojibase offers several alternatives for many emoji, which is why this returns a list
        // rather than a single string.
        Catalog.GetShortcodes("™", ShortcodePreset.Emojibase).Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void GetShortcodes_IsEmptyRatherThanNull_ForGapsAndNonsense() {
        // Only the CLDR preset covers every emoji; the rest are partial.
        Catalog.GetShortcodes("not an emoji").Should().BeEmpty();
        Catalog.GetShortcodes(null).Should().BeEmpty();
    }

    [Fact]
    public void CldrPreset_CoversEveryEmoji_WhichIsWhyItIsTheDefault() {
        var uncovered = Catalog.All
            .Where(e => Catalog.GetShortcodes(e.Sequence, ShortcodePreset.Cldr).IsEmpty)
            .Select(e => $"{e.Sequence} {e.Hexcode} {e.Label}")
            .Take(10)
            .ToList();

        uncovered.Should().BeEmpty();
    }

    [Fact]
    public void EveryShippedPreset_IsReachable() {
        // Each embedded preset must resolve something, or a preset is present in the pack but
        // unreachable through the enum. A typo in the name mapping would look exactly like that,
        // and nothing else in the suite would catch it.
        foreach (var preset in Enum.GetValues<ShortcodePreset>()) {
            var covered = Catalog.AllIncludingComponents.Count(e => !Catalog.GetShortcodes(e.Sequence, preset).IsEmpty);

            covered.Should().BeGreaterThan(0, "preset {0} should cover at least one emoji", preset);
        }
    }

    [Fact]
    public void CldrNative_IsADiacriticOverlay_NotAFullSet() {
        // Worth pinning because the name suggests otherwise. For English it holds exactly the
        // handful of names whose accents the plain CLDR set strips, so a caller must fall back to
        // Cldr rather than treating CldrNative as a complete vocabulary.
        Catalog.GetShortcodes("\U0001F1F9\U0001F1F7", ShortcodePreset.Cldr).Should().Contain("flag_turkiye");
        Catalog.GetShortcodes("\U0001F1F9\U0001F1F7", ShortcodePreset.CldrNative).Should().Contain("flag_türkiye");

        var covered = Catalog.AllIncludingComponents
            .Count(e => !Catalog.GetShortcodes(e.Sequence, ShortcodePreset.CldrNative).IsEmpty);

        covered.Should().BeLessThan(20, "it is an overlay of accented names, not a vocabulary");
    }

    // ---------------------------------------------------------------------------------------
    // Sequence handling and language independence.
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("\U0001F44D")]              // bare
    [InlineData("\U0001F44D️")]        // fully qualified
    [InlineData("\U0001F44D\U0001F3FD")]    // toned
    public void GetShortcodes_AcceptsAnySpellingOfTheEmoji(string sequence) =>
        Catalog.GetShortcodes(sequence).Should().Contain("thumbs_up");

    [Fact]
    public void Shortcodes_WorkInEveryLanguage() {
        // ":+1:" is not really English, it is a convention. A Ukrainian application still wants it
        // to resolve, so shortcodes are shared rather than shipped per language.
        var ukrainian = EmojiCatalog.Load("uk");

        ukrainian.FindByShortcode(":+1:")!.Hexcode.Should().Be("1F44D");

        // The emoji it resolves to is still described in Ukrainian.
        ukrainian.FindByShortcode(":+1:")!.Label.Should().Be("великі пальці вгору");
    }
}
