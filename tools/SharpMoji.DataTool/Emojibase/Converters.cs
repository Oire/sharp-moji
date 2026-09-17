using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oire.SharpMoji.DataTool.Emojibase;

/// <summary>
/// Reads Emojibase's <c>tone</c> field, which is a bare number for single-tone emoji and an array
/// for the 19 that take two skin-tone modifiers.
/// </summary>
/// <remarks>
/// This is the hazard that made the February 2026 draft's <c>ApplySkinTone(string, SkinTone)</c>
/// unimplementable: a single-tone signature cannot express <c>"tone": [1, 2]</c>. See
/// <c>docs/SPEC.md</c> sections 3.3 and 5.3.
/// </remarks>
public sealed class SkinToneListConverter: JsonConverter<ImmutableArray<int>> {
    public override ImmutableArray<int> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.Number) {
            return [reader.GetInt32()];
        }

        if (reader.TokenType != JsonTokenType.StartArray) {
            throw new JsonException($"Expected a number or an array for 'tone', found {reader.TokenType}.");
        }

        var builder = ImmutableArray.CreateBuilder<int>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
            builder.Add(reader.GetInt32());
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Writes the same shape the source used: a bare number for one tone, an array for more.
    /// </summary>
    /// <remarks>
    /// Round-tripping to the source's own shape is what lets the Phase 0 tests compare against the
    /// upstream JSON directly, rather than against a normalized copy that could hide a loss.
    /// </remarks>
    public override void Write(Utf8JsonWriter writer, ImmutableArray<int> value, JsonSerializerOptions options) {
        if (value.Length == 1) {
            writer.WriteNumberValue(value[0]);
            return;
        }

        writer.WriteStartArray();

        foreach (var tone in value) {
            writer.WriteNumberValue(tone);
        }

        writer.WriteEndArray();
    }
}

/// <summary>
/// Reads Emojibase's <c>emoticon</c> field, which is a string on 35 records and an array of
/// strings on 14 (for example <c>":D"</c> versus <c>["xD", "XD"]</c>).
/// </summary>
/// <remarks>
/// Found during Phase 0. The specification originally recorded three polymorphic fields; this is
/// a fourth.
/// </remarks>
public sealed class EmoticonConverter: JsonConverter<ImmutableArray<string>?> {
    public override ImmutableArray<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.Null) {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String) {
            return [reader.GetString()!];
        }

        if (reader.TokenType != JsonTokenType.StartArray) {
            throw new JsonException($"Expected a string or an array for 'emoticon', found {reader.TokenType}.");
        }

        var builder = ImmutableArray.CreateBuilder<string>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
            builder.Add(reader.GetString()!);
        }

        return builder.ToImmutable();
    }

    public override void Write(Utf8JsonWriter writer, ImmutableArray<string>? value, JsonSerializerOptions options) {
        if (value is not { } emoticons) {
            writer.WriteNullValue();
            return;
        }

        if (emoticons.Length == 1) {
            writer.WriteStringValue(emoticons[0]);
            return;
        }

        writer.WriteStartArray();

        foreach (var emoticon in emoticons) {
            writer.WriteStringValue(emoticon);
        }

        writer.WriteEndArray();
    }
}

/// <summary>
/// Maps Emojibase's <c>text</c> field to <see langword="null"/> when it is the empty string.
/// </summary>
/// <remarks>
/// Upstream writes <c>""</c> rather than omitting the field on 1590 of 1949 English records, where
/// it means "this emoji has no text presentation" — not "its text presentation is the empty
/// string". Writing restores <c>""</c>, so the round trip stays faithful to the source.
/// </remarks>
public sealed class EmptyStringAsNullConverter: JsonConverter<string?> {
    /// <summary>
    /// Opts into being called for null values.
    /// </summary>
    /// <remarks>
    /// Without this, System.Text.Json short-circuits a null and writes <c>null</c> itself, never
    /// calling <see cref="Write"/> — so the empty string upstream actually uses would not be
    /// restored and the round trip would silently alter the data.
    /// </remarks>
    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var value = reader.GetString();

        return string.IsNullOrEmpty(value) ? null : value;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value ?? string.Empty);
}
