using System.Collections.Immutable;

namespace Oire.SharpMoji;

/// <summary>
/// Emoji data in one language.
/// </summary>
/// <remarks>
/// <para>
/// A catalog is immutable and bound to a single language. Getting another language means getting
/// another catalog, which is what makes thread safety structural rather than something this
/// documentation has to promise. An application may hold several at once and search them all.
/// </para>
/// <para>
/// Every member is synchronous. All data is embedded, so there is no I/O to await — see
/// <c>docs/SPEC.md</c> section 5.1.
/// </para>
/// <para>
/// This interface exists so that consumers can substitute a catalog in their own tests.
/// </para>
/// </remarks>
public interface IEmojiCatalog {
    /// <summary>The language this catalog holds text for.</summary>
    EmojiLocale Locale { get; }

    /// <summary>
    /// Every pickable emoji, in canonical display order.
    /// </summary>
    /// <remarks>
    /// Excludes the component group — bare skin-tone and hair modifiers, which are building blocks
    /// rather than things anyone picks. Use <see cref="AllIncludingComponents"/> if you need them.
    /// </remarks>
    ImmutableArray<Emoji> All { get; }

    /// <summary>Every emoji including components, in canonical display order.</summary>
    ImmutableArray<Emoji> AllIncludingComponents { get; }

    /// <summary>The categories, in canonical order, named in this catalog's language.</summary>
    ImmutableArray<EmojiGroup> Groups { get; }

    /// <summary>The subcategories, in canonical order, named in this catalog's language.</summary>
    ImmutableArray<EmojiSubgroup> Subgroups { get; }

    /// <summary>The five skin-tone names in this catalog's language, lightest first.</summary>
    ImmutableArray<string> SkinToneNames { get; }

    /// <summary>
    /// Finds an emoji by its character sequence.
    /// </summary>
    /// <param name="sequence">
    /// The emoji, with or without its trailing variation selector. Both spellings resolve to the
    /// same record; written out they look identical, which is precisely why this method exists.
    /// </param>
    /// <returns>The emoji, or <see langword="null"/> if there is no such emoji.</returns>
    /// <remarks>
    /// Matches skin-tone variants as well as base emoji, so handing it <c>"👍🏽"</c> returns the
    /// thumbs-up record rather than nothing. Variation selectors are normalized away on both
    /// sides, which is what makes pasted input work.
    /// </remarks>
    Emoji? Find(string? sequence);

    /// <summary>Finds an emoji by hexcode, for example <c>"1F44D"</c>.</summary>
    /// <param name="hexcode">The hexcode, case-insensitive.</param>
    /// <returns>The emoji, or <see langword="null"/> if there is no such emoji.</returns>
    /// <remarks>
    /// Hexcode is the stable key, so this is the right lookup for restoring persisted favorites or
    /// recents. Skin-tone variant hexcodes resolve to their base emoji.
    /// </remarks>
    Emoji? FindByHexcode(string? hexcode);

    /// <summary>
    /// Finds an emoji by shortcode, searching every preset.
    /// </summary>
    /// <param name="shortcode">
    /// A shortcode such as <c>"thumbsup"</c>. Surrounding colons are optional, so <c>":+1:"</c>
    /// works as well as <c>"+1"</c> — applications usually have the colons still attached.
    /// </param>
    /// <returns>The emoji, or <see langword="null"/> if no preset claims that shortcode.</returns>
    /// <remarks>
    /// Presets disagree, so searching all of them is the forgiving default for a picker. Pass a
    /// preset explicitly when an application must match one vocabulary exactly — for instance when
    /// round-tripping text that another tool will re-read.
    /// </remarks>
    Emoji? FindByShortcode(string? shortcode);

    /// <summary>Finds an emoji by shortcode within one preset.</summary>
    /// <param name="shortcode">A shortcode, with or without surrounding colons.</param>
    /// <param name="preset">The vocabulary to search.</param>
    /// <returns>The emoji, or <see langword="null"/> if that preset does not define it.</returns>
    Emoji? FindByShortcode(string? shortcode, ShortcodePreset preset);

    /// <summary>Returns an emoji's shortcodes in one preset.</summary>
    /// <param name="sequence">Any spelling of the emoji, or one of its variants.</param>
    /// <param name="preset">The vocabulary to read.</param>
    /// <returns>
    /// The shortcodes, without colons, or empty when that preset does not cover the emoji. Only
    /// <see cref="ShortcodePreset.Cldr"/> covers all of them.
    /// </returns>
    ImmutableArray<string> GetShortcodes(string? sequence, ShortcodePreset preset = ShortcodePreset.Cldr);

    /// <summary>Finds an emoji by a text emoticon such as <c>":D"</c>.</summary>
    /// <param name="emoticon">The emoticon, matched exactly.</param>
    /// <returns>The emoji, or <see langword="null"/> if no emoji claims that emoticon.</returns>
    /// <remarks>Only 49 emoji have emoticons, so most input will find nothing.</remarks>
    Emoji? FindByEmoticon(string? emoticon);

    /// <summary>Returns the emoji in one category, in canonical order.</summary>
    /// <param name="group">A group from <see cref="Groups"/>.</param>
    /// <returns>The emoji in that group, empty if the group is unknown to this catalog.</returns>
    ImmutableArray<Emoji> GetByGroup(EmojiGroup group);

    /// <summary>Returns the emoji in one subcategory, in canonical order.</summary>
    /// <param name="subgroup">A subgroup from <see cref="Subgroups"/>.</param>
    /// <returns>The emoji in that subgroup, empty if the subgroup is unknown to this catalog.</returns>
    ImmutableArray<Emoji> GetBySubgroup(EmojiSubgroup subgroup);

    /// <summary>
    /// Searches this catalog's labels, tags and shortcodes.
    /// </summary>
    /// <param name="query">What the user typed. Case and diacritics are ignored.</param>
    /// <param name="limit">The most results to return.</param>
    /// <returns>Matches, best first, or empty for a blank query.</returns>
    /// <remarks>
    /// <para>
    /// Results are ranked by <see cref="MatchKind"/> — an exact shortcode, then an exact name,
    /// then a name the query starts, and so on — with ties broken by canonical order so the same
    /// query always returns the same list.
    /// </para>
    /// <para>
    /// Matching ignores case and diacritics, so <c>cafe</c> finds <c>café</c>. A query of several
    /// words is treated as a name first and, failing that, as terms that must all appear.
    /// </para>
    /// </remarks>
    ImmutableArray<EmojiMatch> Search(string? query, int limit = 25);

    /// <summary>
    /// How many skin-tone modifiers an emoji accepts: none, one, or one per person.
    /// </summary>
    /// <param name="sequence">Any spelling of the emoji, or one of its variants.</param>
    /// <returns>0, 1 or 2. Unknown input reports 0.</returns>
    /// <remarks>
    /// Two means the sequence depicts two people who can be toned independently — 🤝 and 🧑‍🤝‍🧑 and
    /// their kin, nineteen emoji in all. A picker should offer a tone grid for those and a single
    /// row for everything else.
    /// </remarks>
    int GetSkinToneSlots(string? sequence);

    /// <summary>Returns every skin-tone variant of an emoji, in canonical order.</summary>
    /// <param name="sequence">Any spelling of the emoji, or one of its variants.</param>
    /// <returns>
    /// The variants — five for a one-slot emoji, twenty-five for a two-slot one — or empty when
    /// the emoji takes no tones or is unknown.
    /// </returns>
    ImmutableArray<EmojiSkin> GetSkins(string? sequence);

    /// <summary>
    /// Finds the variant where every person shares one skin tone.
    /// </summary>
    /// <param name="sequence">Any spelling of the emoji, or one of its variants.</param>
    /// <param name="tone">The tone to apply. <see cref="SkinTone.None"/> is not a variant.</param>
    /// <param name="skin">The variant, or <see langword="null"/> when there is none.</param>
    /// <returns><see langword="true"/> when a variant was found.</returns>
    /// <remarks>
    /// Works for one-slot and two-slot emoji alike. For a two-slot emoji it returns the variant
    /// where both people share the tone, which upstream stores with a single modifier rather than
    /// a repeated pair.
    /// </remarks>
    bool TryGetSkin(string? sequence, SkinTone tone, out EmojiSkin? skin);

    /// <summary>
    /// Finds the variant where two people carry the given skin tones.
    /// </summary>
    /// <param name="sequence">Any spelling of the emoji, or one of its variants.</param>
    /// <param name="first">The first person's tone.</param>
    /// <param name="second">The second person's tone.</param>
    /// <param name="skin">The variant, or <see langword="null"/> when there is none.</param>
    /// <returns><see langword="true"/> when a variant was found.</returns>
    /// <remarks>
    /// Passing the same tone twice is accepted and resolves to the matching-tone variant, so
    /// callers can always pass two tones without special-casing the diagonal of their tone grid.
    /// Passing two different tones to a one-slot emoji returns <see langword="false"/> rather than
    /// guessing which one was meant.
    /// </remarks>
    bool TryGetSkin(string? sequence, SkinTone first, SkinTone second, out EmojiSkin? skin);
}
