using FluentAssertions;
using Xunit;

namespace Oire.SharpMoji.Tests;

/// <summary>
/// Phase 2: the public catalog surface.
/// </summary>
/// <remarks>
/// These need no corpus — they exercise the embedded data, which is what consumers get.
/// </remarks>
public class EmojiCatalogTests {
    // Deliberately typed as the interface rather than the concrete class: these tests exercise the
    // surface consumers will actually code against, and a member that slipped off IEmojiCatalog
    // should fail here. CA1859 suggests the concrete type for speed, which is not the point.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1859:Use concrete types when possible",
        Justification = "Testing through the public interface is the intent.")]
    private static readonly IEmojiCatalog English = EmojiCatalog.English;

    [Fact]
    public void English_IsAvailableAndPopulated() {
        English.Locale.Code.Should().Be("en");
        English.All.Should().NotBeEmpty();
        English.Groups.Should().NotBeEmpty();
        English.SkinToneNames.Should().HaveCount(5);
    }

    [Fact]
    public void Load_IsCached_SoRepeatedCallsAreFree() {
        // Callers should be able to write EmojiCatalog.English in a loop without thinking about it.
        EmojiCatalog.Load("uk").Should().BeSameAs(EmojiCatalog.Load("uk"));
        EmojiCatalog.English.Should().BeSameAs(EmojiCatalog.Load("EN"));
    }

    [Fact]
    public void Load_RejectsUnknownLanguages_WithAHelpfulMessage() {
        var act = () => EmojiCatalog.Load("xx");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*not an embedded language*")
            .Which.Message.Should().Contain("uk", "the message should list what is available");
    }

    [Fact]
    public void TryLoad_ReportsFailureInsteadOfThrowing() {
        EmojiCatalog.TryLoad("xx", out var missing).Should().BeFalse();
        missing.Should().BeNull();

        EmojiCatalog.TryLoad("uk", out var found).Should().BeTrue();
        found!.Locale.Code.Should().Be("uk");
    }

    // ---------------------------------------------------------------------------------------
    // Lookup, including the variation-selector problem this API exists to hide.
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("\U0001F44D")]          // as typed or pasted: bare
    [InlineData("\U0001F44D️")]    // as stored: fully qualified
    // Spelled as escapes on purpose. Written literally these two strings look identical, because
    // the only difference is an invisible variation selector - which is the entire problem.
    public void Find_AcceptsBothSpellingsOfTheSameEmoji(string sequence) {
        // The whole point of SPEC 3.6. A dictionary keyed on the stored form would answer "no such
        // emoji" to the first of these, which is the form users actually supply.
        English.Find(sequence)!.Hexcode.Should().Be("1F44D");
    }

    [Fact]
    public void Find_ResolvesSkinToneVariantsToTheirBaseEmoji() {
        // Someone pasting a toned emoji is asking about that emoji, not about nothing.
        English.Find("\U0001F44D\U0001F3FD")!.Hexcode.Should().Be("1F44D");
    }

    [Fact]
    public void Find_ReturnsNullRatherThanThrowing_ForInputThatIsNotAnEmoji() {
        English.Find("not an emoji").Should().BeNull();
        English.Find(string.Empty).Should().BeNull();
        English.Find(null).Should().BeNull();
    }

    [Fact]
    public void FindByHexcode_IsCaseInsensitive_AndResolvesVariants() {
        English.FindByHexcode("1F44D")!.Label.Should().Be("thumbs up");
        English.FindByHexcode("1f44d")!.Hexcode.Should().Be("1F44D");
        English.FindByHexcode("1F44D-1F3FD")!.Hexcode.Should().Be("1F44D");
        English.FindByHexcode("nonsense").Should().BeNull();
    }

    [Fact]
    public void FindByEmoticon_FindsTheFewEmojiThatHaveOne() {
        English.FindByEmoticon(":D").Should().NotBeNull();
        English.FindByEmoticon("definitely not an emoticon").Should().BeNull();
    }

    // ---------------------------------------------------------------------------------------
    // Ordering and grouping.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void All_IsInCanonicalOrder() {
        // A picker shows emoji in this order. Sorting by anything else - or not sorting - produces
        // a layout users perceive as random.
        var ordered = English.All.Where(e => e.Order is not null).Select(e => e.Order!.Value).ToList();

        ordered.Should().BeInAscendingOrder();
    }

    [Fact]
    public void All_ExcludesComponents_ButAllIncludingComponentsDoesNot() {
        // Bare skin-tone and hair modifiers are building blocks, not things anyone picks.
        English.All.Should().NotContain(e => e.Group != null && e.Group.IsComponent);
        English.AllIncludingComponents.Should().Contain(e => e.Group != null && e.Group.IsComponent);
        English.AllIncludingComponents.Length.Should().BeGreaterThan(English.All.Length);
    }

    [Fact]
    public void GetByGroup_ReturnsThatGroupsEmoji() {
        var people = English.Groups.Single(g => g.Key == "people-body");
        var emoji = English.GetByGroup(people);

        emoji.Should().NotBeEmpty();
        emoji.Should().OnlyContain(e => e.Group!.Key == "people-body");
        emoji.Should().Contain(e => e.Hexcode == "1F44D");
    }

    [Fact]
    public void GroupLessRecords_LoadWithoutError() {
        // The 26 regional indicators have no group, which is why the property is nullable.
        English.AllIncludingComponents.Should().Contain(e => e.Group == null);
    }

    // ---------------------------------------------------------------------------------------
    // Localization.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Labels_AreInTheCatalogsLanguage() {
        EmojiCatalog.Load("uk").FindByHexcode("1F44D")!.Label.Should().Be("великі пальці вгору");
        EmojiCatalog.Load("fr").FindByHexcode("1F44D")!.Label.Should().NotBe("thumbs up");
    }

    [Fact]
    public void GroupNames_AreLocalizedToo() {
        // The draft missed this entirely: a Ukrainian picker needs Ukrainian category headings,
        // not English ones over Ukrainian emoji names (SPEC 3.5).
        var ukrainian = EmojiCatalog.Load("uk");
        var english = EmojiCatalog.English;

        var index = english.Groups.Single(g => g.Key == "people-body").Index;

        ukrainian.Groups[index].Key.Should().Be("people-body", "keys are language-independent");
        ukrainian.Groups[index].Name.Should().NotBe(english.Groups[index].Name);
    }

    [Fact]
    public void EveryLanguage_LoadsAndAgreesOnStructure() {
        // Structure is shared; only text differs. If that ever stopped being true, the single
        // structure pack would be wrong for 27 of 28 languages.
        var reference = EmojiCatalog.English.All.Select(e => e.Hexcode).ToList();

        foreach (var locale in EmojiLocale.All) {
            var catalog = EmojiCatalog.Load(locale.Code);

            catalog.All.Select(e => e.Hexcode).Should().Equal(reference,
                "language '{0}' must describe the same emoji as English", locale.Code);
        }
    }

    [Fact]
    public void Locales_CarryUsableDisplayNames() {
        var ukrainian = EmojiLocale.Find("uk")!;

        ukrainian.EnglishName.Should().Be("Ukrainian");
        ukrainian.NativeName.Should().Be("українська");
        ukrainian.IsRightToLeft.Should().BeFalse();

        // Baked in at build time, so they do not depend on the host having ICU data.
        EmojiLocale.All.Should().OnlyContain(l => l.NativeName.Length > 0 && l.EnglishName.Length > 0);
        EmojiLocale.All.Should().OnlyContain(l => l.NativeName != l.Code, "a code is not a name");
    }

    [Fact]
    public void LocaleLookup_IsForgivingAboutCase() {
        EmojiLocale.Find("ZH-HANT")!.Code.Should().Be("zh-hant");
        EmojiLocale.Find("  uk  ")!.Code.Should().Be("uk");
        EmojiLocale.Find("xx").Should().BeNull();
        EmojiLocale.Find(null).Should().BeNull();
    }

    // ---------------------------------------------------------------------------------------
    // Immutability, which is what makes catalogs safe to share.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Catalogs_AreSafeToUseFromManyThreadsAtOnce() {
        // Thread safety here is structural - nothing mutates after construction - but the cache
        // itself is shared, so exercise the path that would expose a torn read.
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 200, i => {
            var catalog = EmojiCatalog.Load(i % 2 == 0 ? "en" : "uk");

            results.Add(catalog.Find("\U0001F44D")!.Hexcode);
        });

        results.Should().HaveCount(200);
        results.Should().OnlyContain(h => h == "1F44D");
    }
}
