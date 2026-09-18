using FluentAssertions;
using Xunit;

namespace Oire.SharpMoji.Tests;

/// <summary>
/// Phase 5: search behavior, as distinct from search quality.
/// </summary>
/// <remarks>
/// Quality lives in <see cref="SearchGoldenCorpusTests"/>. These cover the contract: ordering is
/// total, input is handled forgivingly, and results are stable.
/// </remarks>
public class SearchTests {
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1859:Use concrete types when possible",
        Justification = "Testing through the public interface is the intent.")]
    private static readonly IEmojiCatalog Catalog = EmojiCatalog.English;

    [Fact]
    public void Search_RanksByMatchKind() {
        var results = Catalog.Search("smile", 25);

        results.Should().NotBeEmpty();

        // Kinds must be non-decreasing: every exact match before every prefix, and so on.
        results.Select(r => (int)r.Kind).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Search_IsStable_SoAPickerDoesNotReshuffle() {
        // Results that shuffle between identical queries read as a broken UI, and dictionary
        // iteration order is not something to rely on.
        var first = Catalog.Search("heart", 25).Select(r => r.Emoji.Hexcode);
        var second = Catalog.Search("heart", 25).Select(r => r.Emoji.Hexcode);

        first.Should().Equal(second);
    }

    [Fact]
    public void Search_ReportsWhyEachResultMatched() {
        var results = Catalog.Search("grinning face", 5);

        var top = results[0];

        top.Kind.Should().Be(MatchKind.Label);
        top.MatchedText.Should().Be("grinning face", "a UI should be able to highlight the matched text");
        top.Emoji.Hexcode.Should().Be("1F600");
    }

    [Fact]
    public void Search_RespectsTheLimit() {
        Catalog.Search("face", 5).Should().HaveCount(5);
        Catalog.Search("face", 1).Should().HaveCount(1);
        Catalog.Search("face", 0).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(":")]
    public void Search_ReturnsNothingForEmptyInput(string? query) =>
        Catalog.Search(query, 25).Should().BeEmpty();

    [Fact]
    public void Search_ReturnsNothingRatherThanThrowing_ForNonsense() =>
        Catalog.Search("qqqzzzxxx", 25).Should().BeEmpty();

    [Fact]
    public void Search_ExcludesComponents() {
        // Bare skin-tone modifiers should never appear in a picker's results, even though they
        // carry labels mentioning "skin tone".
        Catalog.Search("skin tone", 25).Should().NotContain(r => r.Emoji.Group != null && r.Emoji.Group.IsComponent);
    }

    [Fact]
    public void Search_FoldsCaseAndDiacritics() {
        var french = EmojiCatalog.Load("fr");

        // Someone who cannot readily type an accent should still find the emoji.
        var withAccent = french.Search("fusée", 5).Select(r => r.Emoji.Hexcode);
        var without = french.Search("fusee", 5).Select(r => r.Emoji.Hexcode);

        without.Should().Equal(withAccent);
    }

    [Fact]
    public void Search_TreatsAMultiWordQueryAsANameFirst() {
        // "grinning face" is a name, not two loose words, so it must match exactly rather than
        // falling through to "anything mentioning grinning and face".
        Catalog.Search("grinning face", 3)[0].Kind.Should().Be(MatchKind.Label);
    }

    [Fact]
    public void Search_FallsBackToRequiringEveryTerm() {
        // No emoji is named "flag turkiye", so the multi-term path runs - and both terms must
        // appear, or every flag in the set would match.
        var results = Catalog.Search("flag turkiye", 10);

        results.Should().NotBeEmpty();
        results.Select(r => r.Emoji.Hexcode).Should().Contain("1F1F9-1F1F7");
    }

    [Fact]
    public void Search_PutsAnExactShortcodeFirst() {
        var results = Catalog.Search("tada", 10);

        results[0].Kind.Should().Be(MatchKind.Shortcode);
        results[0].Emoji.Hexcode.Should().Be("1F389");
    }

    [Fact]
    public void Search_KeepsTheRestOfTheList_WhenTheQueryIsAlsoAWord() {
        // "fire" names 🔥 as a shortcode and is also a word eleven emoji use. The shortcode heads
        // the list, but the list continues - the user may have meant the word.
        var results = Catalog.Search("fire", 10);

        results[0].Kind.Should().Be(MatchKind.Shortcode);
        results.Should().HaveCountGreaterThan(1);
        results.Skip(1).Should().NotContain(r => r.Emoji.Hexcode == results[0].Emoji.Hexcode, "no duplicates");
    }

    [Theory]
    [InlineData(":D", "1F604")]
    [InlineData(":)", "1F642")]
    [InlineData(":-)", "1F642")]
    [InlineData("<3", "2764")]
    public void Search_FindsEmoticonsWithoutBeingAsked(string emoticon, string hexcode) {
        // Someone typing ":D" into a picker's search box means the emoji, not a search for colons.
        var results = Catalog.Search(emoticon, 10);

        results.Should().NotBeEmpty("'{0}' should find something", emoticon);
        results[0].Kind.Should().Be(MatchKind.Emoticon);
        results[0].Emoji.Hexcode.Should().Be(hexcode);
    }

    [Fact]
    public void Search_DoesNotMistakePunctuationForAnEmoticon() {
        // A stray colon or bracket is not a search for anything.
        Catalog.Search(":", 5).Should().BeEmpty();
        Catalog.Search("::", 5).Should().BeEmpty();
    }

    [Fact]
    public void Search_WorksInEveryLanguage() {
        // Every language must return something for its own word for "heart", which catches a
        // language whose string pack failed to load or whose folding broke.
        var queries = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["en"] = "heart",
            ["fr"] = "coeur",
            ["de"] = "herz",
            ["uk"] = "серце",
            ["ru"] = "сердце",
            ["ja"] = "ハート",
            ["pl"] = "serce",
            ["it"] = "cuore",
        };

        foreach (var (locale, query) in queries) {
            EmojiCatalog.Load(locale).Search(query, 5)
                .Should().NotBeEmpty("'{0}' should find something in {1}", query, locale);
        }
    }

    [Fact]
    public void Search_IsFastEnoughForTypeAhead() {
        var catalog = EmojiCatalog.English;

        catalog.Search("warm", 25);   // build the index outside the measurement

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 0; i < 200; i++) {
            catalog.Search("smil", 25);
        }

        stopwatch.Stop();

        // A generous gate. The point is to catch an algorithmic regression - something turning the
        // scan quadratic - not to pin a number to this machine.
        var perQuery = stopwatch.Elapsed.TotalMilliseconds / 200;

        perQuery.Should().BeLessThan(10, "type-ahead runs a query per keystroke");
    }
}
