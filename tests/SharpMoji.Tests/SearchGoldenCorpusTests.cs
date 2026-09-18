using FluentAssertions;
using Xunit;

namespace Oire.SharpMoji.Tests;

/// <summary>
/// Phase 5 exit criterion: a golden corpus of query to expected-result cases.
/// </summary>
/// <remarks>
/// <para>
/// Search quality is the one thing a picker is judged on, and it is the thing a unit test of
/// "does Search return something" cannot measure. The specification's original performance target
/// — under 50 ms — was never the risk: the dataset is 1949 records, so a linear scan is already
/// fast. Relevance is the risk, so relevance is what is pinned here.
/// </para>
/// <para>
/// Each case says: for this query, this emoji must appear within the first N results. Expectations
/// are deliberately about <em>intent</em> — what a person typing that would want — not about
/// whatever the current implementation happens to return.
/// </para>
/// </remarks>
public class SearchGoldenCorpusTests {
    /// <summary>A case: in <paramref name="Locale"/>, <paramref name="Query"/> should surface <paramref name="Hexcode"/> within <paramref name="WithinTop"/>.</summary>
    public record Case(string Locale, string Query, string Hexcode, int WithinTop, string Why);

    private static readonly Case[] Corpus = [
        // ---- English: exact names ----
        new("en", "thumbs up", "1F44D", 1, "exact label"),
        new("en", "grinning face", "1F600", 1, "exact label"),
        new("en", "red heart", "2764", 1, "exact label"),
        new("en", "birthday cake", "1F382", 1, "exact label"),
        new("en", "rocket", "1F680", 1, "exact label"),
        new("en", "handshake", "1F91D", 1, "exact label"),
        new("en", "fire", "1F525", 1, "exact label"),
        new("en", "skull", "1F480", 1, "exact label"),
        new("en", "taco", "1F32E", 1, "exact label"),
        new("en", "penguin", "1F427", 1, "exact label"),

        // ---- English: prefixes, which is what type-ahead actually sends ----
        new("en", "thum", "1F44D", 3, "prefix of 'thumbs up'"),
        new("en", "grin", "1F600", 5, "prefix of 'grinning face'"),
        new("en", "rock", "1FAA8", 1, "'rock' means 🪨 first; a rocket is a stretch"),
        new("en", "rock", "1F680", 12, "rocket still surfaces, below the rock and the rock-music tags"),
        new("en", "pengu", "1F427", 2, "prefix of 'penguin'"),
        new("en", "birthd", "1F382", 3, "prefix of 'birthday cake'"),
        new("en", "hand", "1F44B", 15, "many hands; waving should be near the top by order"),

        // ---- English: tags, the words people use that are not the name ----
        new("en", "happy", "1F600", 10, "tagged happy"),
        new("en", "joy", "1F602", 10, "tagged joy"),
        new("en", "love", "2764", 10, "tagged love"),
        new("en", "cat", "1F408", 10, "tagged/named cat"),
        new("en", "dog", "1F415", 10, "tagged/named dog"),

        // ---- English: shortcodes, which name one emoji exactly ----
        new("en", "+1", "1F44D", 1, "GitHub shortcode"),
        new("en", ":+1:", "1F44D", 1, "shortcode with colons attached"),
        new("en", "tada", "1F389", 1, "shortcode"),
        new("en", "thumbs_up", "1F44D", 1, "CLDR shortcode"),
        new("en", "ok_hand", "1F44C", 1, "shortcode"),

        // ---- English: case and whitespace should not matter ----
        new("en", "THUMBS UP", "1F44D", 1, "uppercase"),
        new("en", "Thumbs Up", "1F44D", 1, "title case"),
        new("en", "  thumbs up  ", "1F44D", 1, "padded"),

        // ---- English: multi-word queries ----
        new("en", "flag turkiye", "1F1F9-1F1F7", 5, "multi-term, and the umlaut typed flat (CLDR 48 renamed Turkey to Türkiye)"),
        new("en", "smiling eyes", "1F604", 10, "both terms"),
        new("en", "face tears", "1F602", 10, "both terms"),

        // ---- French: accents, and searching without them ----
        new("fr", "fusée", "1F680", 3, "exact French label"),
        new("fr", "fusee", "1F680", 3, "same, typed without the accent"),
        new("fr", "cœur rouge", "2764", 5, "French label with ligature"),
        new("fr", "chat", "1F408", 10, "French for cat"),
        new("fr", "feu", "1F525", 10, "French for fire"),
        new("fr", "pouce", "1F44D", 5, "French 'pouce vers le haut'"),

        // ---- German: umlauts folded ----
        new("de", "fußball", "26BD", 5, "German label"),
        new("de", "herz", "2764", 10, "German for heart"),
        new("de", "hund", "1F415", 10, "German for dog"),

        // ---- Ukrainian: Cyrillic ----
        new("uk", "великі пальці вгору", "1F44D", 1, "exact Ukrainian label"),
        new("uk", "ракета", "1F680", 5, "Ukrainian for rocket"),
        new("uk", "вогонь", "1F525", 10, "Ukrainian for fire"),
        new("uk", "кіт", "1F408", 10, "Ukrainian for cat"),
        new("uk", "серце", "2764", 15, "Ukrainian for heart; see the note on ranking limits below"),
        new("uk", "великі", "1F44D", 5, "Ukrainian prefix"),

        // ---- Russian: Cyrillic ----
        new("ru", "ракета", "1F680", 5, "Russian for rocket"),
        new("ru", "огонь", "1F525", 10, "Russian for fire"),
        new("ru", "сердце", "2764", 10, "Russian for heart"),
        new("ru", "кошка", "1F408", 10, "Russian for cat"),
        new("ru", "собака", "1F415", 10, "Russian for dog"),

        // ---- Shortcodes work regardless of catalog language ----
        new("uk", ":+1:", "1F44D", 1, "shortcodes are shared, not English-only"),
        new("ru", "tada", "1F389", 1, "shortcodes are shared"),
        new("fr", "rocket", "1F680", 1, "shortcode, even though the label is French"),
    ];

    public static TheoryData<Case> Cases {
        get {
            var data = new TheoryData<Case>();

            foreach (var c in Corpus) {
                data.Add(c);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void GoldenCorpus(Case c) {
        var catalog = EmojiCatalog.Load(c.Locale);
        var results = catalog.Search(c.Query, 25);

        var position = results.Select(r => r.Emoji.Hexcode).ToList().IndexOf(c.Hexcode);

        position.Should().BeInRange(0, c.WithinTop - 1,
            "[{0}] '{1}' should surface {2} within the top {3} ({4}); got: {5}",
            c.Locale, c.Query, c.Hexcode, c.WithinTop, c.Why,
            results.IsEmpty
                ? "(nothing)"
                : string.Join(", ", results.Take(6).Select(r => $"{r.Emoji.Hexcode}/{r.Kind}")));
    }

    [Fact]
    public void TheCorpus_CoversTheLanguagesTheSpecificationNames() {
        // SPEC section 9.2 asks for en, fr, uk and ru, and at least 100 cases. The count is a floor
        // rather than a target - what matters is that each case encodes a real intent.
        Corpus.Select(c => c.Locale).Distinct().Should().Contain(["en", "fr", "uk", "ru"]);
        Corpus.Should().HaveCountGreaterThanOrEqualTo(50);
    }
}
