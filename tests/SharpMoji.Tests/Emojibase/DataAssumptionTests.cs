using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Oire.SharpMoji.Tests.Emojibase;
using Xunit;

namespace Oire.SharpMoji.Tests;

/// <summary>
/// Phase 0: proves every assumption docs/SPEC.md makes about the Emojibase data.
/// </summary>
/// <remarks>
/// <para>
/// The February 2026 draft specified a data model without ever deserializing the source, and got
/// every field name wrong plus an unimplementable skin-tone API. These tests are the guard against
/// repeating that: each one corresponds to a numbered claim in the specification, so if upstream
/// changes shape, the claim fails here rather than in a consumer's picker.
/// </para>
/// <para>
/// They skip when the corpus has not been fetched. See <see cref="EmojiCorpus"/>.
/// </para>
/// </remarks>
[Trait("Category", "DataAssumptions")]
public class DataAssumptionTests {
    public static TheoryData<string> AllLocales {
        get {
            var data = new TheoryData<string>();

            foreach (var locale in EmojiCorpus.Locales) {
                data.Add(locale);
            }

            return data;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Exit criterion: every locale round-trips losslessly.
    // ---------------------------------------------------------------------------------------

    [SkippableTheory]
    [MemberData(nameof(AllLocales))]
    public void EveryLocale_DeserializesWithNoUnmappedFields(string locale) {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        // EmojiCorpus.Options sets UnmappedMemberHandling.Disallow, so an upstream field the model
        // does not know about throws here rather than vanishing.
        var emoji = EmojiCorpus.Load(locale);

        emoji.Should().NotBeEmpty();
    }

    [SkippableTheory]
    [MemberData(nameof(AllLocales))]
    public void EveryLocale_RoundTripsToSemanticallyIdenticalJson(string locale) {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        var original = EmojiCorpus.ReadRaw(locale);
        var model = EmojiCorpus.Load(locale);
        var written = JsonSerializer.Serialize(model, EmojiCorpus.Options);

        var expected = JsonNode.Parse(original)!;
        var actual = JsonNode.Parse(written)!;

        // Compare the parsed trees rather than the text: upstream formatting and property order are
        // not part of the contract, but every key and value is.
        var differences = JsonTreeComparer.Compare(expected, actual).Take(5).ToList();

        differences.Should().BeEmpty(
            "locale '{0}' must survive a round trip through the model without losing or altering data",
            locale);
    }

    // ---------------------------------------------------------------------------------------
    // SPEC section 3.7 — structure is locale-independent.
    // ---------------------------------------------------------------------------------------

    [SkippableFact]
    public void AllLocales_ShareIdenticalStructure() {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        var reference = EmojiCorpus.Load("en");
        var referenceShape = reference.Select(Shape).ToImmutableArray();

        foreach (var locale in EmojiCorpus.Locales.Where(l => l != "en")) {
            var other = EmojiCorpus.Load(locale);

            other.Length.Should().Be(reference.Length, "locale '{0}' must carry the same records as English", locale);
            other.Select(Shape).Should().Equal(referenceShape,
                "only label and tags may differ between locales — everything else is shared structure (SPEC 3.7)");
        }
    }

    [SkippableTheory]
    [MemberData(nameof(AllLocales))]
    public void EveryLocale_HasTheExpectedRecordCount(string locale) {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        EmojiCorpus.Load(locale).Length.Should().Be(SharpMojiData.EmojiCount);
    }

    // ---------------------------------------------------------------------------------------
    // SPEC section 3.3 — the polymorphic fields.
    // ---------------------------------------------------------------------------------------

    [SkippableFact]
    public void Tone_IsPolymorphic_AndBothFormsAreRead() {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        var skins = EmojiCorpus.Load("en").SelectMany(e => e.Skins ?? []).ToImmutableArray();

        skins.Should().NotBeEmpty();
        skins.Should().Contain(s => s.Tone.Length == 1, "most emoji take a single skin-tone modifier");
        skins.Should().Contain(s => s.Tone.Length == 2, "19 emoji take two (SPEC 5.3)");
        skins.Should().OnlyContain(s => s.Tone.Length == 1 || s.Tone.Length == 2, "no emoji takes three or more");
        skins.Should().OnlyContain(s => s.Tone.All(t => t >= 1 && t <= 5), "tones are Fitzpatrick 1-5");
    }

    [SkippableFact]
    public void DualToneEmoji_AreExactlyNineteen_WithTwentyFiveVariantsEach() {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        var dualTone = EmojiCorpus.Load("en")
            .Where(e => (e.Skins ?? []).Any(s => s.Tone.Length == 2))
            .ToImmutableArray();

        dualTone.Should().HaveCount(19, "this is the count the skin-tone API must handle (SPEC 5.3)");

        foreach (var emoji in dualTone) {
            var skins = emoji.Skins!.Value;

            skins.Should().HaveCount(25, "emoji '{0}' covers a full 5x5 tone matrix", emoji.Label);

            // The matrix is NOT stored as 25 pairs. Matching tones are encoded as a SINGLE modifier
            // applied to the whole sequence - handshake with two light-skinned hands is
            // "1F91D-1F3FB" with "tone": 1, not "tone": [1, 1]. Only the 20 mixed combinations are
            // stored as pairs. An API that looked up (Light, Light) as a pair would find nothing.
            var singles = skins.Where(s => s.Tone.Length == 1).ToImmutableArray();
            var pairs = skins.Where(s => s.Tone.Length == 2).ToImmutableArray();

            singles.Should().HaveCount(5, "emoji '{0}' encodes its five matching-tone variants with a single modifier", emoji.Label);
            pairs.Should().HaveCount(20, "emoji '{0}' stores only the mixed-tone combinations as pairs", emoji.Label);

            singles.Select(s => s.Tone[0]).Should().BeEquivalentTo([1, 2, 3, 4, 5]);
            pairs.Should().OnlyContain(s => s.Tone[0] != s.Tone[1],
                "a pair always denotes two different tones; matching ones are a single modifier");

            // Normalizing a single tone to the pair it stands for must cover the matrix exactly once.
            var combinations = skins
                .Select(s => s.Tone.Length == 1 ? (s.Tone[0], s.Tone[0]) : (s.Tone[0], s.Tone[1]))
                .ToList();

            combinations.Should().OnlyHaveUniqueItems();

            for (var first = 1; first <= 5; first++) {
                for (var second = 1; second <= 5; second++) {
                    combinations.Should().Contain((first, second),
                        "emoji '{0}' must offer tone combination ({1}, {2})", emoji.Label, first, second);
                }
            }
        }
    }

    [SkippableFact]
    public void Version_IsSometimesIntegerAndSometimesFractional() {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        var versions = EmojiCorpus.Load("en").Select(e => e.Version).ToImmutableArray();

        versions.Should().Contain(v => v == Math.Floor(v), "some records use a bare integer");
        versions.Should().Contain(v => v != Math.Floor(v), "others use a fraction such as 0.6");

        // Modeling this as int - as the February 2026 draft did - would throw on every fractional
        // record. No converter is needed; the correct CLR type is enough.
        versions.Should().Contain(0.6);
    }

    [SkippableFact]
    public void Emoticon_IsSometimesAStringAndSometimesAnArray() {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        // Found during Phase 0: a fourth polymorphic field the specification had not recorded.
        var raw = JsonNode.Parse(EmojiCorpus.ReadRaw("en"))!.AsArray();
        var emoticons = raw.Select(n => n!["emoticon"]).Where(n => n is not null).ToList();

        // Evaluated outside the assertion: FluentAssertions builds an expression tree, which cannot
        // contain an `is` pattern.
        emoticons.Any(n => n is JsonValue).Should().BeTrue("35 records carry a bare string");
        emoticons.Any(n => n is JsonArray).Should().BeTrue("14 records carry an array");

        var parsed = EmojiCorpus.Load("en").Where(e => e.Emoticon is not null).ToImmutableArray();

        parsed.Should().HaveCount(emoticons.Count, "both forms must survive deserialization");
        parsed.Should().OnlyContain(e => e.Emoticon!.Value.Length >= 1);
    }

    [SkippableFact]
    public void Text_IsEmptyStringRatherThanAbsent_AndIsNormalizedToNull() {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        var raw = JsonNode.Parse(EmojiCorpus.ReadRaw("en"))!.AsArray();
        var emptyInSource = raw.Count(n => n!["text"]?.GetValue<string>() == string.Empty);

        emptyInSource.Should().BeGreaterThan(1000, "most emoji have no text presentation");

        var nullAfterParsing = EmojiCorpus.Load("en").Count(e => e.Text is null);

        nullAfterParsing.Should().Be(emptyInSource,
            "an empty text presentation means 'none', and must not survive as an empty string");
    }

    // ---------------------------------------------------------------------------------------
    // SPEC section 3.6 — variation selectors.
    // ---------------------------------------------------------------------------------------

    [SkippableFact]
    public void ManyRecords_CarryVariationSelector16_SoLookupsMustNormalize() {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        var emoji = EmojiCorpus.Load("en");
        var qualified = emoji.Where(e => e.Emoji.Contains('️')).ToImmutableArray();

        qualified.Should().HaveCountGreaterThan(500,
            "a naive dictionary keyed on the emoji field would miss every one of these when a user " +
            "pastes the unqualified form (SPEC 3.6)");

        // Thumbs up is the canonical example: the data carries U+1F44D U+FE0F.
        var thumbsUp = emoji.Single(e => e.Hexcode == "1F44D");

        thumbsUp.Emoji.Should().Be("\U0001F44D️");
        thumbsUp.Emoji.Should().NotBe("\U0001F44D", "the stored sequence is qualified, the typed one is not");
    }

    // ---------------------------------------------------------------------------------------
    // SPEC section 3.2 / 3.5 — optional fields and components.
    // ---------------------------------------------------------------------------------------

    [SkippableFact]
    public void SomeRecords_HaveNoGroup_SoTheGroupPropertyMustBeNullable() {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        var groupless = EmojiCorpus.Load("en").Where(e => e.Group is null).ToImmutableArray();

        groupless.Should().NotBeEmpty("a non-nullable group enum would fail to load these (SPEC 3.2)");

        // The same records lack order, subgroup and tags, so all four must be optional together.
        groupless.Should().OnlyContain(e => e.Order == null && e.Subgroup == null && !e.Tags.HasValue);
    }

    [SkippableFact]
    public void ComponentRecords_AreGroupTwo_AndMustBeExcludedFromPickers() {
        Skip.IfNot(EmojiCorpus.IsAvailable, EmojiCorpus.SkipReason);

        var components = EmojiCorpus.Load("en").Where(e => e.Group == 2).ToImmutableArray();

        components.Should().NotBeEmpty();
        components.Should().OnlyContain(e => !e.Skins.HasValue,
            "components are bare modifiers; they do not themselves take skin tones");
    }

    private static string Shape(EmojibaseEmoji e) =>
        string.Join('|',
            e.Hexcode,
            e.Emoji,
            e.Text ?? string.Empty,
            e.Type,
            e.Order,
            e.Group,
            e.Subgroup,
            e.Version,
            e.Gender,
            string.Join(',', (e.Skins ?? []).Select(s => $"{s.Hexcode}:{string.Join('+', s.Tone)}")));
}
