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
}
