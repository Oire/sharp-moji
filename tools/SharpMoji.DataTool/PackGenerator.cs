using System.Collections.Immutable;
using System.Globalization;
using Oire.SharpMoji.Data;
using Oire.SharpMoji.DataTool.Emojibase;

namespace Oire.SharpMoji.DataTool;

/// <summary>
/// Turns the raw Emojibase corpus into SharpMoji's embedded packs.
/// </summary>
/// <remarks>
/// The whole point is the split: one structure pack shared by every language, plus one small
/// string pack per language. See <c>docs/SPEC.md</c> section 3.7 for the measurements that make
/// this worth doing.
/// </remarks>
internal static class PackGenerator {
    /// <summary>English is bundled in the core package; every other locale is a satellite.</summary>
    public const string CoreLocale = "en";

    /// <summary>Builds the locale-independent structure pack from a reference locale.</summary>
    /// <remarks>
    /// English is used as the reference only because it has to come from somewhere: the structure
    /// is byte-identical in all 28 locales, and <see cref="VerifyStructureIsShared"/> proves it
    /// rather than trusting it.
    /// </remarks>
    public static StructurePack BuildStructure(string emojibaseVersion, string unicodeVersion) {
        var emoji = EmojiCorpus.Load(CoreLocale);
        var messages = EmojiCorpus.LoadMessages(CoreLocale);

        return new StructurePack {
            EmojibaseVersion = emojibaseVersion,
            UnicodeVersion = unicodeVersion,
            Emoji = [.. emoji.Select(ToPackEmoji)],
            GroupKeys = OrderedKeys(messages.Groups),
            SubgroupKeys = OrderedKeys(messages.Subgroups),
            SkinToneKeys = BuildSkinToneKeys(messages.SkinTones),
            Locales = BuildLocales(),
        };
    }

    /// <summary>
    /// Builds the locale metadata baked into the structure pack.
    /// </summary>
    /// <remarks>
    /// Names come from <see cref="CultureInfo"/> on the build machine and are then frozen into the
    /// pack, rather than being looked up at run time. That keeps them identical everywhere and
    /// keeps the library working under <c>InvariantGlobalization</c>, where the lookup would
    /// otherwise return the code back as the name.
    /// </remarks>
    private static PackLocale[] BuildLocales() =>
    [
        .. EmojiCorpus.Locales.Select(code => {
            var culture = CultureInfo.GetCultureInfo(ToCultureName(code));

            return new PackLocale {
                Code = code,
                EnglishName = culture.EnglishName,
                NativeName = culture.NativeName,
                IsRightToLeft = culture.TextInfo.IsRightToLeft,
            };
        })
    ];

    /// <summary>
    /// Maps an Emojibase locale code to the .NET culture name for the same language.
    /// </summary>
    /// <remarks>
    /// Emojibase lowercases the whole code; .NET expects a title-cased script subtag and an
    /// uppercase region subtag, so "zh-hant" has to become "zh-Hant" and "en-gb" has to become
    /// "en-GB" or the lookup returns a culture named after the code itself.
    /// </remarks>
    private static string ToCultureName(string locale) {
        var parts = locale.Split('-');

        if (parts.Length == 1) {
            return parts[0];
        }

        // A four-letter subtag is a script (Hant); anything shorter is a region (GB, MX).
        var subtag = parts[1].Length == 4
            ? string.Concat(char.ToUpperInvariant(parts[1][0]), parts[1][1..].ToLowerInvariant())
            : parts[1].ToUpperInvariant();

        return $"{parts[0]}-{subtag}";
    }

    /// <summary>Builds one language's string pack.</summary>
    public static StringPack BuildStrings(string locale) {
        var emoji = EmojiCorpus.Load(locale);
        var messages = EmojiCorpus.LoadMessages(locale);

        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        var tags = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var record in emoji) {
            labels[record.Hexcode] = record.Label;

            if (record.Tags is { } recordTags) {
                tags[record.Hexcode] = [.. recordTags];
            }

            // Skin variants carry their own labels ("thumbs up: light skin tone"), so a picker can
            // announce the chosen tone without composing the string itself.
            foreach (var skin in record.Skins ?? []) {
                labels[skin.Hexcode] = skin.Label;

                if (skin.Tags is { } skinTags) {
                    tags[skin.Hexcode] = [.. skinTags];
                }
            }
        }

        return new StringPack {
            Locale = locale,
            Labels = labels,
            Tags = tags,
            Groups = OrderedMessages(messages.Groups),
            Subgroups = OrderedMessages(messages.Subgroups),
            SkinTones = OrderedSkinToneMessages(messages.SkinTones),
        };
    }

    /// <summary>
    /// Checks that every locale really does share the reference structure.
    /// </summary>
    /// <returns>The locales that disagree, empty when the assumption holds.</returns>
    /// <remarks>
    /// The entire packaging design rests on this. If a future Emojibase release ever localized
    /// something structural, generation must fail loudly rather than silently emit a structure
    /// pack that is wrong for 27 of 28 languages.
    /// </remarks>
    public static ImmutableArray<string> VerifyStructureIsShared() {
        var reference = Fingerprint(EmojiCorpus.Load(CoreLocale));
        var referenceGroups = GroupFingerprint(EmojiCorpus.LoadMessages(CoreLocale));

        var mismatches = ImmutableArray.CreateBuilder<string>();

        foreach (var locale in EmojiCorpus.Locales.Where(l => l != CoreLocale)) {
            if (Fingerprint(EmojiCorpus.Load(locale)) != reference) {
                mismatches.Add($"{locale} (emoji structure)");
            }

            if (GroupFingerprint(EmojiCorpus.LoadMessages(locale)) != referenceGroups) {
                mismatches.Add($"{locale} (group keys)");
            }
        }

        return mismatches.ToImmutable();
    }

    private static PackEmoji ToPackEmoji(EmojibaseEmoji e) => new() {
        Hexcode = e.Hexcode,
        Sequence = e.Emoji,
        TextSequence = e.Text,
        Presentation = e.Type,
        Order = e.Order,
        Group = e.Group,
        Subgroup = e.Subgroup,
        Version = e.Version,
        Gender = e.Gender,
        Emoticons = e.Emoticon is { } emoticons ? [.. emoticons] : null,
        Skins = e.Skins is { Length: > 0 } skins ? [.. skins.Select(ToPackSkin)] : null,
    };

    private static PackSkin ToPackSkin(EmojibaseSkin s) => new() {
        Hexcode = s.Hexcode,
        Sequence = s.Emoji,
        Order = s.Order ?? 0,
        Tones = [.. s.Tone],
        Gender = s.Gender,
    };

    private static string[] OrderedKeys(ImmutableArray<EmojibaseMessage> messages) =>
        [.. Ordered(messages).Select(m => m.Key)];

    private static string[] OrderedMessages(ImmutableArray<EmojibaseMessage> messages) =>
        [.. Ordered(messages).Select(m => m.Message)];

    /// <summary>
    /// Places group and subgroup entries at the index the emoji records point to.
    /// </summary>
    /// <remarks>
    /// Upstream lists them in an arbitrary order and carries the real index in <c>order</c>, so
    /// the list must be rebuilt positionally. Sorting alphabetically, or trusting file order,
    /// would mislabel every category.
    /// </remarks>
    private static ImmutableArray<EmojibaseMessage> Ordered(ImmutableArray<EmojibaseMessage> messages) {
        var size = messages.Max(m => m.Order ?? 0) + 1;
        var slots = new EmojibaseMessage?[size];

        foreach (var message in messages) {
            var index = message.Order ?? 0;

            // Subgroup 'order' is not unique upstream: 101 entries share 100 slots. First writer
            // wins, which matches how the indices are used to look a name up.
            slots[index] ??= message;
        }

        return [.. slots.Select((m, i) => m ?? throw new InvalidDataException(
            $"No message occupies index {i}; group indices would be misaligned."))];
    }

    /// <summary>
    /// Orders skin tones by Fitzpatrick index, so position 0 is tone 1.
    /// </summary>
    /// <remarks>
    /// Upstream lists these unordered and without an <c>order</c> field - the English file starts
    /// with "dark", which is tone 5. Sorting by the documented key order is the only way to line
    /// them up with the numeric tones in the data.
    /// </remarks>
    private static string[] OrderedSkinToneMessages(ImmutableArray<EmojibaseMessage> skinTones) {
        var byKey = skinTones.ToDictionary(m => m.Key, m => m.Message, StringComparer.Ordinal);

        return [.. SkinToneKeyOrder.Select(key => byKey.TryGetValue(key, out var message)
            ? message
            : throw new InvalidDataException($"Skin tone '{key}' is missing from messages.json."))];
    }

    private static string[] BuildSkinToneKeys(ImmutableArray<EmojibaseMessage> skinTones) {
        foreach (var key in SkinToneKeyOrder) {
            if (!skinTones.Any(m => m.Key == key)) {
                throw new InvalidDataException($"Skin tone '{key}' is missing from messages.json.");
            }
        }

        return [.. SkinToneKeyOrder];
    }

    private static readonly ImmutableArray<string> SkinToneKeyOrder =
        ["light", "medium-light", "medium", "medium-dark", "dark"];

    private static string Fingerprint(ImmutableArray<EmojibaseEmoji> emoji) =>
        string.Join('\n', emoji.Select(e => string.Join('|',
            e.Hexcode, e.Emoji, e.Text, e.Type, e.Order, e.Group, e.Subgroup, e.Version, e.Gender,
            string.Join(',', (e.Skins ?? []).Select(s => $"{s.Hexcode}:{string.Join('+', s.Tone)}:{s.Order}")))));

    private static string GroupFingerprint(EmojibaseMessages messages) =>
        string.Join('|', OrderedKeys(messages.Groups)) + "||" + string.Join('|', OrderedKeys(messages.Subgroups));
}
