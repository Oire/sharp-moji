using System.Text.Json.Serialization;

namespace Oire.SharpMoji.Data;

/// <summary>
/// The locale-independent emoji structure, shared by every language.
/// </summary>
/// <remarks>
/// <para>
/// All 28 Emojibase locales carry byte-identical structure and differ only in labels and tags
/// (<c>docs/SPEC.md</c> section 3.7). Storing it once instead of 28 times is what brings the
/// complete bundle from 26.5 MB down to 1,360 KB, and is why SharpMoji can embed every language
/// rather than downloading any.
/// </para>
/// <para>
/// Property names are abbreviated because this is generated, machine-read data: the names repeat
/// 1949 times and shortening them measurably shrinks the compressed resource. The JSON is never
/// meant to be hand-edited — regenerate it with <c>scripts/update-emoji-data.ps1</c>.
/// </para>
/// </remarks>
internal sealed class StructurePack {
    [JsonPropertyName("eb")]
    public required string EmojibaseVersion { get; init; }

    [JsonPropertyName("uni")]
    public required string UnicodeVersion { get; init; }

    [JsonPropertyName("e")]
    public required PackEmoji[] Emoji { get; init; }

    /// <summary>Locale-independent group keys, indexed by the value of <see cref="PackEmoji.Group"/>.</summary>
    [JsonPropertyName("g")]
    public required string[] GroupKeys { get; init; }

    /// <summary>Locale-independent subgroup keys, indexed by <see cref="PackEmoji.Subgroup"/>.</summary>
    [JsonPropertyName("sg")]
    public required string[] SubgroupKeys { get; init; }

    /// <summary>Skin-tone keys in Fitzpatrick order, so index 0 is tone 1.</summary>
    [JsonPropertyName("st")]
    public required string[] SkinToneKeys { get; init; }

    /// <summary>Metadata for every embedded language.</summary>
    /// <remarks>
    /// Baked in at generation time rather than derived from <c>CultureInfo</c> at run time, so the
    /// names are identical on every machine and survive <c>InvariantGlobalization</c>.
    /// </remarks>
    [JsonPropertyName("loc")]
    public required PackLocale[] Locales { get; init; }
}

/// <summary>One embedded language's metadata.</summary>
internal sealed class PackLocale {
    [JsonPropertyName("c")]
    public required string Code { get; init; }

    [JsonPropertyName("e")]
    public required string EnglishName { get; init; }

    [JsonPropertyName("n")]
    public required string NativeName { get; init; }

    /// <summary>Omitted for the left-to-right majority, which is most of the bytes saved here.</summary>
    [JsonPropertyName("r")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsRightToLeft { get; init; }
}

/// <summary>One emoji's structure, with no localized text.</summary>
internal sealed class PackEmoji {
    [JsonPropertyName("h")]
    public required string Hexcode { get; init; }

    /// <summary>The sequence as published, which may carry U+FE0F.</summary>
    [JsonPropertyName("s")]
    public required string Sequence { get; init; }

    /// <summary>The text-presentation sequence, absent when there is none.</summary>
    [JsonPropertyName("t")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TextSequence { get; init; }

    [JsonPropertyName("p")]
    public int Presentation { get; init; }

    /// <summary>Canonical display order. Renumbers between Unicode versions — never persist it.</summary>
    [JsonPropertyName("o")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Order { get; init; }

    /// <summary>Index into <see cref="StructurePack.GroupKeys"/>. Absent on 26 records.</summary>
    [JsonPropertyName("g")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Group { get; init; }

    [JsonPropertyName("sg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Subgroup { get; init; }

    [JsonPropertyName("v")]
    public double Version { get; init; }

    [JsonPropertyName("gd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Gender { get; init; }

    [JsonPropertyName("em")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Emoticons { get; init; }

    [JsonPropertyName("k")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PackSkin[]? Skins { get; init; }
}

/// <summary>One skin-tone variant.</summary>
/// <remarks>
/// <see cref="Tones"/> holds a single tone when every person in the sequence shares it, and two
/// when they differ. Upstream encodes matching tones with one modifier rather than a repeated
/// pair, so there is no <c>[1, 1]</c> — see <c>docs/SPEC.md</c> section 5.3.
/// </remarks>
internal sealed class PackSkin {
    [JsonPropertyName("h")]
    public required string Hexcode { get; init; }

    [JsonPropertyName("s")]
    public required string Sequence { get; init; }

    [JsonPropertyName("o")]
    public int Order { get; init; }

    [JsonPropertyName("tn")]
    public required int[] Tones { get; init; }

    [JsonPropertyName("gd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Gender { get; init; }
}

/// <summary>
/// One language's text: everything that varies between locales, and nothing that does not.
/// </summary>
internal sealed class StringPack {
    [JsonPropertyName("l")]
    public required string Locale { get; init; }

    /// <summary>Hexcode to label, covering base emoji and skin-tone variants alike.</summary>
    [JsonPropertyName("lb")]
    public required Dictionary<string, string> Labels { get; init; }

    /// <summary>Hexcode to search tags. Absent for records upstream gives no tags.</summary>
    [JsonPropertyName("tg")]
    public required Dictionary<string, string[]> Tags { get; init; }

    /// <summary>Localized group names, positionally matching <see cref="StructurePack.GroupKeys"/>.</summary>
    [JsonPropertyName("g")]
    public required string[] Groups { get; init; }

    [JsonPropertyName("sg")]
    public required string[] Subgroups { get; init; }

    [JsonPropertyName("st")]
    public required string[] SkinTones { get; init; }
}
