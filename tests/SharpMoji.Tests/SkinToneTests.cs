using FluentAssertions;
using Xunit;

namespace Oire.SharpMoji.Tests;

/// <summary>
/// Phase 3: skin-tone lookup.
/// </summary>
/// <remarks>
/// The exit criterion is that all 19 two-slot emoji resolve all 25 of their variants. That is the
/// part the February 2026 draft could not express at all: its
/// <c>ApplySkinTone(string, SkinTone)</c> had one tone parameter, and 19 emoji need two.
/// </remarks>
public class SkinToneTests {
    // Typed as the interface on purpose: these tests exercise the surface consumers code against,
    // so a member that slipped off IEmojiCatalog should fail here. CA1859 wants the concrete type
    // for speed, which is not what this is for.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1859:Use concrete types when possible",
        Justification = "Testing through the public interface is the intent.")]
    private static readonly IEmojiCatalog Catalog = EmojiCatalog.English;

    private const string ThumbsUp = "\U0001F44D";
    private const string Handshake = "\U0001F91D";
    private const string GrinningFace = "\U0001F600";

    // ---------------------------------------------------------------------------------------
    // Slots.
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(GrinningFace, 0)]   // a face has no skin to tone
    [InlineData(ThumbsUp, 1)]       // one hand, one tone
    [InlineData(Handshake, 2)]      // two hands, independently toned
    public void GetSkinToneSlots_ReportsHowManyTonesAnEmojiTakes(string sequence, int expected) =>
        Catalog.GetSkinToneSlots(sequence).Should().Be(expected);

    [Fact]
    public void GetSkinToneSlots_ReportsZeroForInputItDoesNotKnow() {
        Catalog.GetSkinToneSlots("not an emoji").Should().Be(0);
        Catalog.GetSkinToneSlots(null).Should().Be(0);
    }

    [Fact]
    public void ExactlyNineteenEmoji_TakeTwoTones() {
        var twoSlot = Catalog.All.Where(e => Catalog.GetSkinToneSlots(e.Sequence) == 2).ToList();

        twoSlot.Should().HaveCount(19);
        twoSlot.Should().OnlyContain(e => e.Skins.Length == 25);
    }

    // ---------------------------------------------------------------------------------------
    // One tone.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TryGetSkin_FindsTheTonedVariantOfAOneSlotEmoji() {
        Catalog.TryGetSkin(ThumbsUp, SkinTone.Medium, out var skin).Should().BeTrue();

        skin!.Sequence.Should().Be("\U0001F44D\U0001F3FD");
        skin.Hexcode.Should().Be("1F44D-1F3FD");
        skin.Label.Should().Be("thumbs up: medium skin tone");
        skin.Tones.Should().Equal(SkinTone.Medium);
    }

    [Fact]
    public void TryGetSkin_FindsEveryToneForEveryOneSlotEmoji() {
        var tones = new[] { SkinTone.Light, SkinTone.MediumLight, SkinTone.Medium, SkinTone.MediumDark, SkinTone.Dark };

        foreach (var emoji in Catalog.All.Where(e => Catalog.GetSkinToneSlots(e.Sequence) == 1)) {
            foreach (var tone in tones) {
                Catalog.TryGetSkin(emoji.Sequence, tone, out var skin).Should().BeTrue(
                    "{0} should offer {1}", emoji.Label, tone);
                skin!.Tones.Should().Equal(tone);
            }
        }
    }

    [Fact]
    public void TryGetSkin_RejectsNone_WhichIsTheAbsenceOfAToneRatherThanOne() {
        Catalog.TryGetSkin(ThumbsUp, SkinTone.None, out var skin).Should().BeFalse();
        skin.Should().BeNull();
    }

    [Fact]
    public void TryGetSkin_ReturnsFalseForEmojiThatTakeNoTones() {
        Catalog.TryGetSkin(GrinningFace, SkinTone.Dark, out var skin).Should().BeFalse();
        skin.Should().BeNull();
    }

    // ---------------------------------------------------------------------------------------
    // Two tones — the part the draft could not express.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TryGetSkin_FindsMixedTonePairs() {
        Catalog.TryGetSkin(Handshake, SkinTone.Light, SkinTone.MediumLight, out var skin).Should().BeTrue();

        skin!.Tones.Should().Equal(SkinTone.Light, SkinTone.MediumLight);
        skin.Hexcode.Should().Contain("1F3FB").And.Contain("1F3FC");
    }

    [Fact]
    public void TryGetSkin_TreatsMatchingTonesAsTheSingleModifierVariant() {
        // The finding from Phase 0: there is no [Light, Light] pair in the data. Both hands light
        // is stored with ONE modifier. A lookup searching for a matching pair would find nothing,
        // silently losing five of every two-slot emoji's twenty-five variants.
        Catalog.TryGetSkin(Handshake, SkinTone.Light, SkinTone.Light, out var pair).Should().BeTrue();
        Catalog.TryGetSkin(Handshake, SkinTone.Light, out var single).Should().BeTrue();

        pair.Should().Be(single, "both spellings name the same variant");

        // One modifier, not a repeated pair. Asserted with ContainSingle because the collection
        // overload of Equal takes params, and a "because" string would be read as another element.
        pair!.Tones.Should().ContainSingle().Which.Should().Be(SkinTone.Light);
    }

    [Fact]
    public void EveryTwoSlotEmoji_ResolvesAllTwentyFiveCombinations() {
        // The Phase 3 exit criterion, stated exactly.
        var tones = new[] { SkinTone.Light, SkinTone.MediumLight, SkinTone.Medium, SkinTone.MediumDark, SkinTone.Dark };
        var twoSlot = Catalog.All.Where(e => Catalog.GetSkinToneSlots(e.Sequence) == 2).ToList();

        twoSlot.Should().HaveCount(19);

        foreach (var emoji in twoSlot) {
            var found = new HashSet<string>(StringComparer.Ordinal);

            foreach (var first in tones) {
                foreach (var second in tones) {
                    Catalog.TryGetSkin(emoji.Sequence, first, second, out var skin).Should().BeTrue(
                        "{0} should offer ({1}, {2})", emoji.Label, first, second);

                    found.Add(skin!.Hexcode);
                }
            }

            found.Should().HaveCount(25, "{0}'s combinations must all be distinct", emoji.Label);
        }
    }

    [Fact]
    public void TryGetSkin_RefusesTwoDifferentTonesOnAOneSlotEmoji() {
        // There is only one hand to tone. Returning false is honest; picking one of the two
        // silently would be a guess the caller cannot detect.
        Catalog.TryGetSkin(ThumbsUp, SkinTone.Light, SkinTone.Dark, out var skin).Should().BeFalse();
        skin.Should().BeNull();
    }

    // ---------------------------------------------------------------------------------------
    // Input handling.
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("\U0001F44D")]                    // bare
    [InlineData("\U0001F44D️")]              // fully qualified
    [InlineData("\U0001F44D\U0001F3FF")]          // already toned
    public void SkinToneLookups_AcceptAnySpellingOfTheEmoji(string sequence) {
        // Including an already-toned sequence: asking for a different tone on an emoji the user
        // already toned is the single most common thing a picker does.
        Catalog.TryGetSkin(sequence, SkinTone.Medium, out var skin).Should().BeTrue();
        skin!.Hexcode.Should().Be("1F44D-1F3FD");
    }

    [Fact]
    public void GetSkins_ReturnsVariantsInCanonicalOrder() {
        var skins = Catalog.GetSkins(ThumbsUp);

        skins.Should().HaveCount(5);
        skins.Select(s => s.Order).Should().BeInAscendingOrder();
        skins.Select(s => s.Tones[0]).Should().Equal(
            SkinTone.Light, SkinTone.MediumLight, SkinTone.Medium, SkinTone.MediumDark, SkinTone.Dark);
    }

    [Fact]
    public void GetSkins_IsEmptyRatherThanNull_ForEmojiWithoutTones() {
        Catalog.GetSkins(GrinningFace).Should().BeEmpty();
        Catalog.GetSkins("not an emoji").Should().BeEmpty();
        Catalog.GetSkins(null).Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------------------
    // Localization.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void VariantLabels_AreLocalized_SoAPickerCanAnnounceTheTone() {
        // A screen reader should say the tone in the user's language, not compose it from English
        // fragments. The variant carries its own full label for exactly that reason.
        EmojiCatalog.Load("uk").TryGetSkin(ThumbsUp, SkinTone.Dark, out var ukrainian).Should().BeTrue();

        ukrainian!.Label.Should().NotBe("thumbs up: dark skin tone");
        ukrainian.Label.Should().Contain("шкіри");
    }

    [Fact]
    public void SkinToneNames_AreLocalizedAndOrderedLightestFirst() {
        Catalog.SkinToneNames.Should().Equal(
            "light skin tone", "medium-light skin tone", "medium skin tone",
            "medium-dark skin tone", "dark skin tone");

        EmojiCatalog.Load("uk").SkinToneNames.Should().NotEqual(Catalog.SkinToneNames);
    }
}
