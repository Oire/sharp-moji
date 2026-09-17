using FluentAssertions;
using Oire.SharpMoji.Data;
using Oire.SharpMoji.DataTool;
using Oire.SharpMoji.DataTool.Emojibase;
using Xunit;

namespace Oire.SharpMoji.Tests;

/// <summary>
/// Phase 1: the embedded packs are faithful to the corpus, reproducible, and within budget.
/// </summary>
/// <remarks>
/// The packs are generated once per Emojibase release and committed, so nothing rebuilds them on
/// a normal build. That makes them exactly the kind of artifact that can drift out of sync with
/// its source unnoticed — hence these tests.
/// </remarks>
public class PackGenerationTests {
    // -------------------------------------------------------------------------------------
    // The embedded resources, which need no corpus and therefore never skip.
    // -------------------------------------------------------------------------------------

    [Fact]
    public void Structure_IsEmbeddedAndMatchesThePinnedRelease() {
        var structure = EmbeddedPackReader.ReadStructure();

        structure.EmojibaseVersion.Should().Be(SharpMojiData.EmojibaseVersion);
        structure.UnicodeVersion.Should().Be(SharpMojiData.UnicodeVersion);
        structure.Emoji.Should().HaveCount(SharpMojiData.EmojiCount);
    }

    [Fact]
    public void EveryEmojibaseLocale_IsEmbeddedSomewhere() {
        var embedded = EmbeddedPackReader.AvailableLocales();

        embedded.Should().BeEquivalentTo(EmojiCorpus.Locales,
            "shipping every language is the point of the structure/string split (SPEC 7.1)");
    }

    [Fact]
    public void EveryPack_ShipsInTheOneAssembly() {
        // One assembly, deliberately. A satellite package holding the other languages is deleted
        // outright by PublishTrimmed and NativeAOT, because nothing statically references it -
        // leaving a trimmed application with English and no error to explain why.
        var resources = typeof(SharpMojiData).Assembly.GetManifestResourceNames();

        resources.Should().Contain(n => n.EndsWith(".structure.br", StringComparison.Ordinal));
        resources.Should().Contain(n => n.EndsWith(".strings.en.br", StringComparison.Ordinal));
        resources.Should().Contain(n => n.EndsWith(".strings.uk.br", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryEmbeddedLocale_DecompressesAndCoversEveryEmoji() {
        var structure = EmbeddedPackReader.ReadStructure();

        // Labels exist for base emoji and for skin variants alike.
        var expected = structure.Emoji.Length + structure.Emoji.Sum(e => e.Skins?.Length ?? 0);

        foreach (var locale in EmbeddedPackReader.AvailableLocales()) {
            EmbeddedPackReader.TryReadStrings(locale, out var strings).Should().BeTrue();

            strings!.Locale.Should().Be(locale);
            strings.Labels.Should().HaveCount(expected, "locale '{0}' must label every emoji and variant", locale);
            strings.Groups.Should().HaveSameCount(structure.GroupKeys);
            strings.Subgroups.Should().HaveSameCount(structure.SubgroupKeys);
            strings.SkinTones.Should().HaveCount(5);

            foreach (var emoji in structure.Emoji) {
                strings.Labels.Should().ContainKey(emoji.Hexcode);
            }
        }
    }

    [Fact]
    public void GroupNames_AreLocalized_AndAlignedWithTheirIndices() {
        var structure = EmbeddedPackReader.ReadStructure();

        EmbeddedPackReader.TryReadStrings("uk", out var ukrainian).Should().BeTrue();
        EmbeddedPackReader.TryReadStrings("en", out var english).Should().BeTrue();

        // Upstream lists groups in arbitrary order and carries the real index in `order`, so a
        // generator that trusted file order would mislabel every category.
        var peopleIndex = Array.IndexOf(structure.GroupKeys, "people-body");

        peopleIndex.Should().BeGreaterThanOrEqualTo(0);
        english!.Groups[peopleIndex].Should().Be("people & body");
        ukrainian!.Groups[peopleIndex].Should().NotBe(english.Groups[peopleIndex],
            "a Ukrainian picker needs Ukrainian category headings (SPEC 3.5)");

        // Thumbs up is in people-body, and its stored index must resolve to that name.
        var thumbsUp = structure.Emoji.Single(e => e.Hexcode == "1F44D");

        thumbsUp.Group.Should().Be(peopleIndex);
    }

    [Fact]
    public void SkinToneNames_AreOrderedByFitzpatrickIndex() {
        var structure = EmbeddedPackReader.ReadStructure();

        EmbeddedPackReader.TryReadStrings("en", out var english).Should().BeTrue();

        // Upstream lists skin tones unordered and without an `order` field - English starts with
        // "dark", which is tone 5. Position 0 must mean tone 1.
        structure.SkinToneKeys.Should().Equal("light", "medium-light", "medium", "medium-dark", "dark");
        english!.SkinTones[0].Should().Be("light skin tone");
        english.SkinTones[4].Should().Be("dark skin tone");
    }

    [Fact]
    public void BundleSize_IsWithinBudget() {
        // Ship gate from docs/SPEC.md section 12: the whole point of the structure/string split is
        // that every language fits in the box, so downloading is never needed.
        PackResourceSizes().Sum().Should().BeLessThan(1_600_000);
    }

    // -------------------------------------------------------------------------------------
    // Fidelity and reproducibility, which need the corpus.
    // -------------------------------------------------------------------------------------

    [SkippableFact]
    public void Generation_IsDeterministic() {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        // Regenerating the same release twice must produce identical bytes, or a reviewer cannot
        // tell a real data change from serializer noise in the diff.
        var first = EmojiPackSerializer.Write(
            PackGenerator.BuildStructure(SharpMojiData.EmojibaseVersion, SharpMojiData.UnicodeVersion));
        var second = EmojiPackSerializer.Write(
            PackGenerator.BuildStructure(SharpMojiData.EmojibaseVersion, SharpMojiData.UnicodeVersion));

        first.Should().Equal(second);

        var firstStrings = EmojiPackSerializer.Write(PackGenerator.BuildStrings("uk"));
        var secondStrings = EmojiPackSerializer.Write(PackGenerator.BuildStrings("uk"));

        firstStrings.Should().Equal(secondStrings);
    }

    [SkippableFact]
    public void EmbeddedPacks_MatchWhatTheGeneratorProducesFromTheCorpus() {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        // The committed resources must be what the current generator emits. Otherwise a
        // regeneration that was only half-applied - or a hand edit - ships silently.
        var regenerated = PackGenerator.BuildStructure(
            SharpMojiData.EmojibaseVersion, SharpMojiData.UnicodeVersion);
        var embedded = EmbeddedPackReader.ReadStructure();

        embedded.Emoji.Should().HaveCount(regenerated.Emoji.Length);
        embedded.GroupKeys.Should().Equal(regenerated.GroupKeys);
        embedded.SubgroupKeys.Should().Equal(regenerated.SubgroupKeys);

        for (var i = 0; i < regenerated.Emoji.Length; i++) {
            embedded.Emoji[i].Hexcode.Should().Be(regenerated.Emoji[i].Hexcode);
            embedded.Emoji[i].Sequence.Should().Be(regenerated.Emoji[i].Sequence);
            embedded.Emoji[i].Skins?.Length.Should().Be(regenerated.Emoji[i].Skins?.Length);
        }

        EmbeddedPackReader.TryReadStrings("uk", out var embeddedStrings).Should().BeTrue();

        var regeneratedStrings = PackGenerator.BuildStrings("uk");

        embeddedStrings!.Labels.Should().BeEquivalentTo(regeneratedStrings.Labels);
    }

    [SkippableFact]
    public void StructureIsSharedByEveryLocale_WhichTheWholeDesignDependsOn() {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        PackGenerator.VerifyStructureIsShared().Should().BeEmpty(
            "one structure table serves all 29 languages; if that stopped holding, 28 of them " +
            "would be silently mislabeled (SPEC 3.7)");
    }

    [SkippableFact]
    public void SkinToneStructure_SurvivesGeneration() {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        var structure = EmbeddedPackReader.ReadStructure();
        var dualTone = structure.Emoji
            .Where(e => (e.Skins ?? []).Any(s => s.Tones.Length == 2))
            .ToArray();

        dualTone.Should().HaveCount(19);

        foreach (var emoji in dualTone) {
            emoji.Skins!.Should().HaveCount(25);
            emoji.Skins!.Count(s => s.Tones.Length == 1).Should().Be(5,
                "matching tones stay encoded as a single modifier (SPEC 5.3)");
            emoji.Skins!.Count(s => s.Tones.Length == 2).Should().Be(20);
        }
    }

    private static IEnumerable<long> PackResourceSizes() {
        var assembly = typeof(SharpMojiData).Assembly;

        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".br", StringComparison.Ordinal))) {
            using var stream = assembly.GetManifestResourceStream(name)!;

            yield return stream.Length;
        }
    }
}
