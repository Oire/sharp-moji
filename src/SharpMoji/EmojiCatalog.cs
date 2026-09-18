using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using Oire.SharpMoji.Data;

namespace Oire.SharpMoji;

/// <summary>
/// Emoji data in one language, built from the embedded packs.
/// </summary>
/// <remarks>
/// <para>
/// Construction is single-phase: a catalog handed to you is already loaded. There is no
/// <c>new</c> followed by an initialize call, and therefore no window in which a catalog exists
/// but cannot answer questions.
/// </para>
/// <para>
/// Loading decompresses about 45 KB of Brotli and builds the lookup indexes, which takes a few
/// milliseconds. Catalogs are cached per language, so asking twice costs nothing the second time.
/// </para>
/// </remarks>
public sealed class EmojiCatalog: IEmojiCatalog {
    private static readonly ConcurrentDictionary<string, EmojiCatalog> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lazy<StructurePack> Structure = new(EmbeddedPackReader.ReadStructure);

    private readonly FrozenLookup _bySequence;
    private readonly FrozenLookup _byHexcode;
    private readonly FrozenLookup _byEmoticon;
    private readonly ImmutableDictionary<string, ImmutableArray<Emoji>> _byGroup;
    private readonly ImmutableDictionary<string, ImmutableArray<Emoji>> _bySubgroup;
    private readonly Lazy<SearchIndex> _search;

    /// <inheritdoc />
    public EmojiLocale Locale { get; }

    /// <inheritdoc />
    public ImmutableArray<Emoji> All { get; }

    /// <inheritdoc />
    public ImmutableArray<Emoji> AllIncludingComponents { get; }

    /// <inheritdoc />
    public ImmutableArray<EmojiGroup> Groups { get; }

    /// <inheritdoc />
    public ImmutableArray<EmojiSubgroup> Subgroups { get; }

    /// <inheritdoc />
    public ImmutableArray<string> SkinToneNames { get; }

    /// <summary>The English catalog, which every installation carries.</summary>
    public static EmojiCatalog English => Load("en");

    /// <summary>
    /// Loads the catalog for a language.
    /// </summary>
    /// <param name="locale">A language code such as <c>"uk"</c>, or an <see cref="EmojiLocale.Code"/>.</param>
    /// <returns>The catalog, cached after the first call.</returns>
    /// <exception cref="ArgumentException"><paramref name="locale"/> is not an embedded language.</exception>
    /// <remarks>Use <see cref="TryLoad"/> when the code comes from user input or a settings file.</remarks>
    public static EmojiCatalog Load(string locale) {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        return TryLoad(locale, out var catalog)
            ? catalog!
            : throw new ArgumentException(
                $"'{locale}' is not an embedded language. Available: {string.Join(", ", EmojiLocale.All.Select(l => l.Code))}.",
                nameof(locale));
    }

    /// <summary>Loads the catalog for a language, if there is one.</summary>
    /// <param name="locale">A language code, matched case-insensitively.</param>
    /// <param name="catalog">The catalog, or <see langword="null"/> when the language is unknown.</param>
    /// <returns><see langword="true"/> when the language is embedded.</returns>
    public static bool TryLoad(string? locale, out EmojiCatalog? catalog) {
        var resolved = EmojiLocale.Find(locale);

        if (resolved is null) {
            catalog = null;
            return false;
        }

        catalog = Cache.GetOrAdd(resolved.Code, static code => new EmojiCatalog(code));
        return true;
    }

    private EmojiCatalog(string localeCode) {
        var structure = Structure.Value;

        if (!EmbeddedPackReader.TryReadStrings(localeCode, out var strings)) {
            throw new InvalidOperationException(
                $"The string pack for '{localeCode}' is missing from the assembly. Run scripts/update-emoji-data.ps1.");
        }

        Locale = EmojiLocale.Find(localeCode)!;

        Groups = BuildGroups(structure.GroupKeys, strings!.Groups);
        Subgroups = BuildSubgroups(structure.SubgroupKeys, strings.Subgroups);
        SkinToneNames = [.. strings.SkinTones];

        var groupsByIndex = Groups.ToDictionary(g => g.Index);
        var subgroupsByIndex = Subgroups.ToDictionary(g => g.Index);

        var all = ImmutableArray.CreateBuilder<Emoji>(structure.Emoji.Length);

        foreach (var packed in structure.Emoji) {
            all.Add(Build(packed, strings, groupsByIndex, subgroupsByIndex));
        }

        // Canonical order, with the unordered stragglers last rather than dropped. OrderBy is
        // stable, so records sharing an order keep their source sequence.
        AllIncludingComponents = [.. all.OrderBy(e => e.Order ?? int.MaxValue)];
        All = [.. AllIncludingComponents.Where(e => e.Group is not { IsComponent: true })];

        _bySequence = BuildSequenceIndex(AllIncludingComponents);
        _byHexcode = BuildHexcodeIndex(AllIncludingComponents);
        _byEmoticon = BuildEmoticonIndex(AllIncludingComponents);
        _byGroup = GroupBy(All, e => e.Group?.Key);
        _bySubgroup = GroupBy(All, e => e.Subgroup?.Key);

        // Folding every label and tag costs a few milliseconds, so it waits until something
        // actually searches. An application that only renders a grid never pays for it.
        var searchable = All;
        _search = new Lazy<SearchIndex>(() => new SearchIndex(searchable));
    }

    /// <inheritdoc />
    public Emoji? Find(string? sequence) =>
        string.IsNullOrEmpty(sequence) ? null : _bySequence.Find(EmojiSequences.Normalize(sequence));

    /// <inheritdoc />
    public Emoji? FindByHexcode(string? hexcode) =>
        string.IsNullOrWhiteSpace(hexcode) ? null : _byHexcode.Find(hexcode.Trim());

    /// <inheritdoc />
    public Emoji? FindByShortcode(string? shortcode) {
        if (Normalize(shortcode) is not { } normalized) {
            return null;
        }

        // Presets are searched in declaration order, so Cldr wins a disagreement. It is the only
        // preset covering every emoji, which makes it the least surprising tie-breaker.
        foreach (var preset in ShortcodeIndex.Presets) {
            if (ShortcodeIndex.Find(preset, normalized) is { } hexcode && FindByHexcode(hexcode) is { } emoji) {
                return emoji;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public Emoji? FindByShortcode(string? shortcode, ShortcodePreset preset) =>
        Normalize(shortcode) is { } normalized
        && ShortcodeIndex.Find(preset, normalized) is { } hexcode
            ? FindByHexcode(hexcode)
            : null;

    /// <inheritdoc />
    public ImmutableArray<string> GetShortcodes(string? sequence, ShortcodePreset preset = ShortcodePreset.Cldr) =>
        Find(sequence) is { } emoji ? ShortcodeIndex.For(preset, emoji.Hexcode) : [];

    /// <summary>
    /// Strips the colons applications usually leave attached, and lowercases.
    /// </summary>
    /// <remarks>
    /// Text being scanned for emoji arrives as <c>":+1:"</c>, not <c>"+1"</c>. Requiring callers to
    /// trim first would make the common case the awkward one.
    /// </remarks>
    private static string? Normalize(string? shortcode) {
        if (string.IsNullOrWhiteSpace(shortcode)) {
            return null;
        }

        var trimmed = shortcode.Trim().Trim(':');

        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>
    /// Removes the "nose" from an emoticon, so <c>:-)</c> finds what <c>:)</c> finds.
    /// </summary>
    /// <remarks>
    /// Upstream carries only the noseless spellings — <c>:)</c>, <c>:D</c>, <c>;P</c> — while
    /// plenty of people type the nosed form. Verified safe against the full set of 64: none
    /// contains a hyphen, and stripping hyphens produces no collisions, so this can never turn one
    /// emoticon into a different one.
    /// </remarks>
    private static string RemoveNose(string emoticon) =>
        emoticon.Contains('-', StringComparison.Ordinal)
            ? emoticon.Replace("-", string.Empty, StringComparison.Ordinal)
            : emoticon;

    /// <inheritdoc />
    public Emoji? FindByEmoticon(string? emoticon) =>
        string.IsNullOrWhiteSpace(emoticon) ? null : _byEmoticon.Find(RemoveNose(emoticon.Trim()));

    /// <inheritdoc />
    public ImmutableArray<Emoji> GetByGroup(EmojiGroup group) {
        ArgumentNullException.ThrowIfNull(group);

        return _byGroup.TryGetValue(group.Key, out var emoji) ? emoji : [];
    }

    /// <inheritdoc />
    public ImmutableArray<Emoji> GetBySubgroup(EmojiSubgroup subgroup) {
        ArgumentNullException.ThrowIfNull(subgroup);

        return _bySubgroup.TryGetValue(subgroup.Key, out var emoji) ? emoji : [];
    }

    /// <inheritdoc />
    public ImmutableArray<EmojiMatch> Search(string? query, int limit = 25) {
        if (string.IsNullOrWhiteSpace(query)) {
            return [];
        }

        // A shortcode or an emoticon names one emoji outright, so it heads the list rather than
        // competing with everything that merely mentions the word. Typing "tada" should give 🎉,
        // and ":D" should give 😄 - neither is a description to be ranked against others.
        //
        // Emoticons are checked first: ":D" and ":-)" are punctuation, so they cannot collide with
        // a shortcode, and checking them first keeps the intent obvious.
        var trimmed = query.Trim();

        EmojiMatch? exact =
            FindByEmoticon(trimmed) is { } byEmoticon
                ? new EmojiMatch { Emoji = byEmoticon, Kind = MatchKind.Emoticon, MatchedText = trimmed }
                : FindByShortcode(trimmed) is { } byShortcode
                    ? new EmojiMatch { Emoji = byShortcode, Kind = MatchKind.Shortcode, MatchedText = trimmed.Trim(':') }
                    : null;

        if (exact is null) {
            return _search.Value.Search(query, limit);
        }

        if (limit == 1) {
            return [exact];
        }

        // The text search still runs, so the exact hit heads a full list rather than replacing it:
        // the user may have meant the word as well as the name.
        return [exact, .. _search.Value.Search(query, limit - 1).Where(m => m.Emoji != exact.Emoji)];
    }

    /// <inheritdoc />
    public int GetSkinToneSlots(string? sequence) {
        if (Find(sequence) is not { } emoji || emoji.Skins.IsEmpty) {
            return 0;
        }

        // Two slots exactly when some variant carries two tones. Counting modifiers in the sequence
        // would be the obvious alternative and is wrong: a matching-tone variant of a two-person
        // emoji carries only one modifier.
        return emoji.Skins.Any(s => s.Tones.Length == 2) ? 2 : 1;
    }

    /// <inheritdoc />
    public ImmutableArray<EmojiSkin> GetSkins(string? sequence) =>
        Find(sequence) is { } emoji ? emoji.Skins : [];

    /// <inheritdoc />
    public bool TryGetSkin(string? sequence, SkinTone tone, out EmojiSkin? skin) {
        skin = null;

        if (tone == SkinTone.None || Find(sequence) is not { } emoji) {
            return false;
        }

        // The single-modifier form, which means "everyone in this sequence, this tone". It exists
        // for one-person and two-person emoji alike, which is why one overload serves both.
        foreach (var candidate in emoji.Skins) {
            if (candidate.Tones.Length == 1 && candidate.Tones[0] == tone) {
                skin = candidate;
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public bool TryGetSkin(string? sequence, SkinTone first, SkinTone second, out EmojiSkin? skin) {
        // Matching tones are stored with one modifier, not as [tone, tone] - there is no such pair
        // in the data. Normalizing here means a caller can iterate a 5x5 grid and pass every cell
        // without having to know that the diagonal is encoded differently.
        if (first == second) {
            return TryGetSkin(sequence, first, out skin);
        }

        skin = null;

        if (first == SkinTone.None || second == SkinTone.None || Find(sequence) is not { } emoji) {
            return false;
        }

        foreach (var candidate in emoji.Skins) {
            if (candidate.Tones.Length == 2 && candidate.Tones[0] == first && candidate.Tones[1] == second) {
                skin = candidate;
                return true;
            }
        }

        return false;
    }

    private static Emoji Build(
        PackEmoji packed,
        StringPack strings,
        Dictionary<int, EmojiGroup> groups,
        Dictionary<int, EmojiSubgroup> subgroups) => new() {
            Sequence = packed.Sequence,
            Hexcode = packed.Hexcode,
            Label = Label(strings, packed.Hexcode),
            Tags = Tags(strings, packed.Hexcode),
            TextSequence = packed.TextSequence,
            Presentation = (EmojiPresentation)packed.Presentation,
            Order = packed.Order,
            Group = packed.Group is { } g && groups.TryGetValue(g, out var group) ? group : null,
            Subgroup = packed.Subgroup is { } s && subgroups.TryGetValue(s, out var subgroup) ? subgroup : null,
            UnicodeVersion = packed.Version,
            Emoticons = packed.Emoticons is { Length: > 0 } emoticons ? [.. emoticons] : [],
            Gender = packed.Gender is { } gender ? (EmojiGender)gender : null,
            Skins = packed.Skins is { Length: > 0 } skins
                ? [.. skins.Select(skin => BuildSkin(skin, strings))]
                : [],
        };

    private static EmojiSkin BuildSkin(PackSkin packed, StringPack strings) => new() {
        Sequence = packed.Sequence,
        Hexcode = packed.Hexcode,
        Label = Label(strings, packed.Hexcode),
        Tones = [.. packed.Tones.Select(t => (SkinTone)t)],
        Order = packed.Order,
        Gender = packed.Gender is { } gender ? (EmojiGender)gender : null,
    };

    private static string Label(StringPack strings, string hexcode) =>
        strings.Labels.TryGetValue(hexcode, out var label) ? label : hexcode;

    private static ImmutableArray<string> Tags(StringPack strings, string hexcode) =>
        strings.Tags.TryGetValue(hexcode, out var tags) ? [.. tags] : [];

    private static ImmutableArray<EmojiGroup> BuildGroups(string[] keys, string[] names) =>
    [
        .. keys.Select((key, index) => new EmojiGroup
        {
            Key = key,
            Name = index < names.Length ? names[index] : key,
            Index = index,
        })
    ];

    private static ImmutableArray<EmojiSubgroup> BuildSubgroups(string[] keys, string[] names) =>
    [
        .. keys.Select((key, index) => new EmojiSubgroup
        {
            Key = key,
            Name = index < names.Length ? names[index] : key,
            Index = index,
        })
    ];

    /// <summary>
    /// Indexes every emoji by sequence, including its skin-tone variants.
    /// </summary>
    /// <remarks>
    /// Keys are normalized, so both the qualified and unqualified forms of the same emoji land on
    /// one entry. Variants map to their base emoji, which is what a caller pasting <c>"👍🏽"</c>
    /// almost always wants.
    /// </remarks>
    private static FrozenLookup BuildSequenceIndex(ImmutableArray<Emoji> emoji) {
        var map = new Dictionary<string, Emoji>(emoji.Length * 2, StringComparer.Ordinal);

        foreach (var item in emoji) {
            map.TryAdd(EmojiSequences.Normalize(item.Sequence), item);

            if (item.TextSequence is { Length: > 0 } text) {
                map.TryAdd(EmojiSequences.Normalize(text), item);
            }

            foreach (var skin in item.Skins) {
                map.TryAdd(EmojiSequences.Normalize(skin.Sequence), item);
            }
        }

        return new FrozenLookup(map, StringComparer.Ordinal);
    }

    private static FrozenLookup BuildHexcodeIndex(ImmutableArray<Emoji> emoji) {
        var map = new Dictionary<string, Emoji>(emoji.Length * 2, StringComparer.OrdinalIgnoreCase);

        foreach (var item in emoji) {
            map.TryAdd(item.Hexcode, item);

            foreach (var skin in item.Skins) {
                map.TryAdd(skin.Hexcode, item);
            }
        }

        return new FrozenLookup(map, StringComparer.OrdinalIgnoreCase);
    }

    private static FrozenLookup BuildEmoticonIndex(ImmutableArray<Emoji> emoji) {
        var map = new Dictionary<string, Emoji>(64, StringComparer.Ordinal);

        foreach (var item in emoji) {
            foreach (var emoticon in item.Emoticons) {
                map.TryAdd(emoticon, item);
            }
        }

        return new FrozenLookup(map, StringComparer.Ordinal);
    }

    private static ImmutableDictionary<string, ImmutableArray<Emoji>> GroupBy(
        ImmutableArray<Emoji> emoji,
        Func<Emoji, string?> key) =>
        emoji
            .Where(e => key(e) is not null)
            .GroupBy(e => key(e)!, StringComparer.Ordinal)
            .ToImmutableDictionary(g => g.Key, g => g.ToImmutableArray(), StringComparer.Ordinal);

    /// <summary>
    /// A read-only string-keyed lookup built once at construction.
    /// </summary>
    /// <remarks>
    /// Wraps <see cref="FrozenDictionary{TKey, TValue}"/>, which trades a slower build for faster
    /// reads — exactly the right trade for a catalog that is built once and then queried for the
    /// lifetime of the application.
    /// </remarks>
    private sealed class FrozenLookup {
        private readonly FrozenDictionary<string, Emoji> _entries;

        public FrozenLookup(Dictionary<string, Emoji> entries, StringComparer comparer) =>
            _entries = entries.ToFrozenDictionary(comparer);

        public Emoji? Find(string key) => _entries.GetValueOrDefault(key);
    }
}
