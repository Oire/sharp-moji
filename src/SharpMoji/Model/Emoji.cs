using System.Collections.Immutable;

namespace Oire.SharpMoji;

/// <summary>
/// One emoji, with its text in the catalog's language.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. Instances come from an <see cref="IEmojiCatalog"/> and are safe to share across
/// threads, cache, or hand to a UI.
/// </para>
/// <para>
/// <see cref="Hexcode"/> is the only stable identifier. <see cref="Order"/> renumbers whenever
/// Unicode inserts emoji, so it is for sorting within a session and must never be persisted —
/// see <c>docs/SPEC.md</c> section 7.4.
/// </para>
/// </remarks>
public sealed record Emoji {
    /// <summary>The emoji as published, for example <c>"👍️"</c>.</summary>
    /// <remarks>
    /// This is the fully-qualified form and may end in U+FE0F. Do not compare it against user
    /// input directly: 517 of 1949 records carry that selector while a typed or pasted emoji
    /// usually does not. <see cref="IEmojiCatalog.Find"/> handles the difference.
    /// </remarks>
    public required string Sequence { get; init; }

    /// <summary>The code points as hyphen-separated hex, for example <c>"1F44D"</c>.</summary>
    /// <remarks>The stable key. This is what to persist for favorites, recents or settings.</remarks>
    public required string Hexcode { get; init; }

    /// <summary>The name in the catalog's language, for example <c>"thumbs up"</c>.</summary>
    public required string Label { get; init; }

    /// <summary>Search keywords in the catalog's language.</summary>
    /// <remarks>Empty for the 26 records upstream gives no tags, never <see langword="null"/>.</remarks>
    public ImmutableArray<string> Tags { get; init; } = [];

    /// <summary>The monochrome text-presentation form, or <see langword="null"/> when there is none.</summary>
    public string? TextSequence { get; init; }

    /// <summary>How this sequence renders by default.</summary>
    public EmojiPresentation Presentation { get; init; }

    /// <summary>The canonical display position, or <see langword="null"/> for unordered records.</summary>
    /// <remarks>Not stable across Unicode releases. Sort by it; do not store it.</remarks>
    public int? Order { get; init; }

    /// <summary>The category, or <see langword="null"/> for the 26 records that have none.</summary>
    public EmojiGroup? Group { get; init; }

    /// <summary>The finer category, or <see langword="null"/> where there is none.</summary>
    public EmojiSubgroup? Subgroup { get; init; }

    /// <summary>The Unicode emoji version that introduced this sequence, such as <c>0.6</c>.</summary>
    /// <remarks>
    /// Fractional for the oldest emoji, which is why this is a <see cref="double"/>. Useful for
    /// hiding sequences that a target platform's fonts will not have.
    /// </remarks>
    public double UnicodeVersion { get; init; }

    /// <summary>Text emoticons that conventionally mean this emoji, such as <c>":D"</c>.</summary>
    /// <remarks>Only 49 emoji have any. Empty otherwise, never <see langword="null"/>.</remarks>
    public ImmutableArray<string> Emoticons { get; init; } = [];

    /// <summary>The gender the sequence encodes, or <see langword="null"/> when it encodes none.</summary>
    public EmojiGender? Gender { get; init; }

    /// <summary>The skin-tone variants of this emoji, empty when it takes none.</summary>
    /// <remarks>
    /// For the 19 emoji that take two modifiers this holds 25 entries: five where both people
    /// share a tone, and twenty mixed pairs. See <see cref="EmojiSkin"/>.
    /// </remarks>
    public ImmutableArray<EmojiSkin> Skins { get; init; } = [];

    /// <summary>Whether this emoji has skin-tone variants at all.</summary>
    public bool SupportsSkinTones => !Skins.IsEmpty;

    /// <summary>Returns <see cref="Sequence"/>, so string interpolation prints the emoji.</summary>
    public override string ToString() => Sequence;
}

/// <summary>
/// One skin-tone variant of an emoji.
/// </summary>
/// <remarks>
/// <see cref="Tones"/> holds one tone when everyone in the sequence shares it and two when they
/// differ. Upstream encodes a matching pair with a single modifier rather than a repeated one, so
/// there is no <c>[Light, Light]</c> entry — the single-tone entry is that variant. Callers that
/// want a uniform view should use <c>IEmojiCatalog</c>'s skin-tone lookups rather than reading
/// <see cref="Tones"/> directly.
/// </remarks>
public sealed record EmojiSkin {
    /// <summary>The variant as published, for example <c>"👍🏽"</c>.</summary>
    public required string Sequence { get; init; }

    /// <summary>The variant's code points, for example <c>"1F44D-1F3FD"</c>.</summary>
    public required string Hexcode { get; init; }

    /// <summary>The variant's name, for example <c>"thumbs up: medium skin tone"</c>.</summary>
    public required string Label { get; init; }

    /// <summary>The tone or tones applied: one entry when they match, two when they differ.</summary>
    public ImmutableArray<SkinTone> Tones { get; init; } = [];

    /// <summary>The canonical display position among its siblings.</summary>
    public int Order { get; init; }

    /// <summary>The gender the variant encodes, or <see langword="null"/> when it encodes none.</summary>
    public EmojiGender? Gender { get; init; }

    /// <summary>Returns <see cref="Sequence"/>.</summary>
    public override string ToString() => Sequence;
}
