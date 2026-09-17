using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Oire.SharpMoji.DataTool.Emojibase;

/// <summary>
/// A record in an Emojibase <c>data.json</c> file, modeled exactly as the upstream JSON is shaped.
/// </summary>
/// <remarks>
/// <para>
/// This is the <em>wire</em> model, not SharpMoji's public model. It exists to prove that the
/// upstream format can be read without loss (Phase 0, <c>docs/SPEC.md</c> section 10) and will be
/// promoted into the build-time data tool in Phase 1. It is deliberately faithful to Emojibase's
/// field names rather than to SharpMoji's, so that a mismatch shows up here rather than being
/// quietly papered over by a rename.
/// </para>
/// <para>
/// The February 2026 draft invented a field name for every one of these. See
/// <c>docs/SPEC.md</c> section 6 for the mapping.
/// </para>
/// </remarks>
public sealed record EmojibaseEmoji {
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("hexcode")]
    public required string Hexcode { get; init; }

    [JsonPropertyName("emoji")]
    public required string Emoji { get; init; }

    [JsonPropertyName("tags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<string>? Tags { get; init; }

    /// <summary>The text-presentation sequence, or <see langword="null"/> when there is none.</summary>
    /// <remarks>
    /// Upstream writes <c>""</c> rather than omitting the field on 1590 of 1949 English records.
    /// <see cref="EmptyStringAsNullConverter"/> normalizes that to <see langword="null"/>, which is
    /// the one deliberate departure from the wire format.
    /// </remarks>
    [JsonPropertyName("text")]
    [JsonConverter(typeof(EmptyStringAsNullConverter))]
    public string? Text { get; init; }

    [JsonPropertyName("type")]
    public int Type { get; init; }

    [JsonPropertyName("order")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Order { get; init; }

    [JsonPropertyName("group")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Group { get; init; }

    [JsonPropertyName("subgroup")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Subgroup { get; init; }

    /// <summary>The Unicode emoji version this character was introduced in.</summary>
    /// <remarks>
    /// Serialized as an integer on some records and a float on others (<c>1</c> vs <c>0.6</c>).
    /// Modeling it as <see cref="double"/> handles both natively — no converter is needed, but an
    /// <see cref="int"/> here would throw on more than half the corpus.
    /// </remarks>
    [JsonPropertyName("version")]
    public double Version { get; init; }

    [JsonPropertyName("emoticon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(EmoticonConverter))]
    public ImmutableArray<string>? Emoticon { get; init; }

    [JsonPropertyName("gender")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Gender { get; init; }

    [JsonPropertyName("skins")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<EmojibaseSkin>? Skins { get; init; }
}

/// <summary>
/// A skin-tone variant nested inside <see cref="EmojibaseEmoji.Skins"/>.
/// </summary>
public sealed record EmojibaseSkin {
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("hexcode")]
    public required string Hexcode { get; init; }

    [JsonPropertyName("emoji")]
    public required string Emoji { get; init; }

    [JsonPropertyName("tags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<string>? Tags { get; init; }

    [JsonPropertyName("text")]
    [JsonConverter(typeof(EmptyStringAsNullConverter))]
    public string? Text { get; init; }

    [JsonPropertyName("type")]
    public int Type { get; init; }

    [JsonPropertyName("order")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Order { get; init; }

    [JsonPropertyName("group")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Group { get; init; }

    [JsonPropertyName("subgroup")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Subgroup { get; init; }

    [JsonPropertyName("version")]
    public double Version { get; init; }

    /// <summary>
    /// The gender of the depicted person, where the sequence encodes one.
    /// </summary>
    /// <remarks>
    /// Present on 520 of 2030 English skin records. Easy to miss, because the field appears on the
    /// nested skins as well as on the top-level record — which is how Phase 0 caught it.
    /// </remarks>
    [JsonPropertyName("gender")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Gender { get; init; }

    /// <summary>
    /// The skin tone or tones applied, as Fitzpatrick indices 1-5.
    /// </summary>
    /// <remarks>
    /// The load-bearing hazard: this is a bare number for single-tone emoji and an array for the
    /// 19 that take two modifiers. <see cref="SkinToneListConverter"/> reads both into a list, so
    /// the rest of the code never has to care which form the source used.
    /// </remarks>
    [JsonPropertyName("tone")]
    [JsonConverter(typeof(SkinToneListConverter))]
    public ImmutableArray<int> Tone { get; init; }
}
