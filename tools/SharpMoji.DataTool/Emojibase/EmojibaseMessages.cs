using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Oire.SharpMoji.DataTool.Emojibase;

/// <summary>
/// An Emojibase <c>messages.json</c> file: the localized names behind the numeric group,
/// subgroup and skin-tone indices used in <c>data.json</c>.
/// </summary>
public sealed record EmojibaseMessages {
    [JsonPropertyName("groups")]
    public required ImmutableArray<EmojibaseMessage> Groups { get; init; }

    [JsonPropertyName("subgroups")]
    public required ImmutableArray<EmojibaseMessage> Subgroups { get; init; }

    [JsonPropertyName("skinTones")]
    public required ImmutableArray<EmojibaseMessage> SkinTones { get; init; }
}

/// <summary>One localized name, with the stable key and index it belongs to.</summary>
/// <remarks>
/// <see cref="Key"/> is locale-independent and belongs in the structure pack; <see cref="Message"/>
/// is the localized text and belongs in the string pack. Splitting them is the same principle that
/// keeps the whole bundle small (docs/SPEC.md section 3.7).
/// </remarks>
public sealed record EmojibaseMessage {
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// The index this entry occupies. Absent on skin tones, which upstream lists unordered.
    /// </summary>
    [JsonPropertyName("order")]
    public int? Order { get; init; }
}
