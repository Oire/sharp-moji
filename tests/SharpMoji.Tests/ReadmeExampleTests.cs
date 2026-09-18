using FluentAssertions;
using Xunit;

namespace Oire.SharpMoji.Tests;

/// <summary>
/// Executes the examples printed in README.md.
/// </summary>
/// <remarks>
/// README examples are the first thing anyone runs and the first thing to rot, because nothing
/// compiles them. These assert the exact outputs the README claims, so a rename or a ranking
/// change that would make the documentation lie fails the build instead.
/// </remarks>
public class ReadmeExampleTests {
    [Fact]
    public void Usage_Section() {
        var emoji = EmojiCatalog.English;

        emoji.Find("\U0001F44D")?.Label.Should().Be("thumbs up");
        emoji.FindByHexcode("1F600")?.Label.Should().Be("grinning face");
        emoji.FindByShortcode(":+1:")?.Label.Should().Be("thumbs up");
        emoji.FindByEmoticon(":D")?.Label.Should().Be("grinning face with smiling eyes");
    }

    [Fact]
    public void Browsing_Section() {
        var emoji = EmojiCatalog.English;

        var groups = emoji.Groups.Where(g => !g.IsComponent).ToList();

        groups.Should().NotBeEmpty();
        groups.Should().OnlyContain(g => emoji.GetByGroup(g).Length > 0);
    }

    [Fact]
    public void Search_Section() {
        var results = EmojiCatalog.English.Search("thum", limit: 5);

        results[0].Emoji.Sequence.Should().StartWith("\U0001F44D");
        results[0].Emoji.Label.Should().Be("thumbs up");
        results[0].Kind.Should().Be(MatchKind.LabelPrefix);

        results[1].Emoji.Label.Should().Be("thumbs down");
        results[1].Kind.Should().Be(MatchKind.LabelPrefix);
    }

    [Fact]
    public void Search_FoldingClaims() {
        // "cafe finds café, and :-) finds :)"
        var french = EmojiCatalog.Load("fr");

        french.Search("cafe", 5).Select(m => m.Emoji)
            .Should().Equal(french.Search("café", 5).Select(m => m.Emoji));

        // Compared by emoji rather than by match: MatchedText deliberately echoes what the user
        // typed, so the nosed and noseless spellings produce different — and correct — records.
        EmojiCatalog.English.Search(":-)", 3).Select(m => m.Emoji)
            .Should().Equal(EmojiCatalog.English.Search(":)", 3).Select(m => m.Emoji));
    }

    [Fact]
    public void SkinTones_Section() {
        var emoji = EmojiCatalog.English;

        emoji.GetSkinToneSlots("\U0001F44D").Should().Be(1);
        emoji.GetSkinToneSlots("\U0001F91D").Should().Be(2);
        emoji.GetSkinToneSlots("\U0001F600").Should().Be(0);

        emoji.TryGetSkin("\U0001F44D", SkinTone.Medium, out var skin).Should().BeTrue();
        skin!.Sequence.Should().Be("\U0001F44D\U0001F3FD");
        skin.Label.Should().Be("thumbs up: medium skin tone");

        emoji.TryGetSkin("\U0001F91D", SkinTone.Light, SkinTone.MediumLight, out var pair).Should().BeTrue();
        pair.Should().NotBeNull();
    }

    [Fact]
    public void OtherLanguages_Section() {
        var uk = EmojiCatalog.Load("uk");

        uk.FindByHexcode("1F44D")?.Label.Should().Be("великі пальці вгору");
        uk.Groups[1].Name.Should().Be("люди");
        uk.Search("серце", 5).Should().NotBeEmpty();
        uk.FindByShortcode(":+1:").Should().NotBeNull();

        EmojiLocale.All.Should().HaveCount(28);
    }

    [Fact]
    public void DetailsThatBite_Section() {
        var emoji = EmojiCatalog.English;

        // "517 of 1949 records store a sequence ending in U+FE0F"
        emoji.AllIncludingComponents.Should().HaveCount(1949);
        emoji.AllIncludingComponents.Count(e => e.Sequence.Contains('️')).Should().Be(517);

        // "Nineteen emoji ... giving 25 variants each"
        var twoSlot = emoji.All.Where(e => emoji.GetSkinToneSlots(e.Sequence) == 2).ToList();

        twoSlot.Should().HaveCount(19);
        twoSlot.Should().OnlyContain(e => e.Skins.Length == 25);

        // "Matching tones are stored with a single modifier rather than a repeated pair"
        twoSlot.Should().OnlyContain(e => e.Skins.Count(s => s.Tones.Length == 1) == 5);
    }

    [Fact]
    public void TheClaimedPackageSize_IsHonest() {
        // "all 28 languages are embedded in about 1.4 MB"
        var assembly = typeof(SharpMojiData).Assembly;
        var total = assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".br", StringComparison.Ordinal))
            .Sum(n => {
                using var stream = assembly.GetManifestResourceStream(n)!;

                return stream.Length;
            });

        (total / 1024.0 / 1024.0).Should().BeInRange(1.2, 1.5, "the README says about 1.4 MB");
    }
}
