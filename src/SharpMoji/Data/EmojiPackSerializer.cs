using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Oire.SharpMoji.Data;

/// <summary>
/// Reads and writes Brotli-compressed emoji packs.
/// </summary>
/// <remarks>
/// A pack is UTF-8 JSON compressed with Brotli at <see cref="CompressionLevel.SmallestSize"/>.
/// Compression matters here: the English string table is 301 KB of JSON that becomes roughly
/// 45 KB, which is the difference between shipping one language and shipping all of them.
/// </remarks>
internal static class EmojiPackSerializer {
    /// <summary>
    /// Serializer settings for packs, chosen so generation is byte-reproducible.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The resolver is the source-generated context, so this stays trimming- and AOT-safe.
    /// </para>
    /// <para>
    /// <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> matters more than its name
    /// suggests: the default encoder escapes every non-ASCII character, which would turn each
    /// emoji and every Cyrillic, Thai or CJK label into six-character <c>\uXXXX</c> sequences.
    /// For data that is almost entirely non-ASCII that is a large and pointless cost, both in
    /// JSON size and after compression. "Unsafe" refers to embedding output directly in HTML,
    /// which is not what these packs are for.
    /// </para>
    /// </remarks>
    private static readonly JsonSerializerOptions PackOptions = new() {
        TypeInfoResolver = EmojiPackJsonContext.Default,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private static JsonTypeInfo<T> TypeInfo<T>() => (JsonTypeInfo<T>)PackOptions.GetTypeInfo(typeof(T));

    /// <summary>Compresses a structure pack.</summary>
    public static byte[] Write(StructurePack pack) =>
        Compress(JsonSerializer.SerializeToUtf8Bytes(pack, TypeInfo<StructurePack>()));

    /// <summary>Compresses a string pack.</summary>
    public static byte[] Write(StringPack pack) =>
        Compress(JsonSerializer.SerializeToUtf8Bytes(pack, TypeInfo<StringPack>()));

    /// <summary>Decompresses and parses a structure pack.</summary>
    public static StructurePack ReadStructure(Stream compressed) =>
        JsonSerializer.Deserialize(Decompress(compressed), TypeInfo<StructurePack>())
        ?? throw new InvalidDataException("The structure pack is empty or malformed.");

    /// <summary>Decompresses and parses a string pack.</summary>
    public static StringPack ReadStrings(Stream compressed) =>
        JsonSerializer.Deserialize(Decompress(compressed), TypeInfo<StringPack>())
        ?? throw new InvalidDataException("The string pack is empty or malformed.");

    private static byte[] Compress(byte[] json) {
        using var output = new MemoryStream();

        // Scoped so the Brotli stream is flushed and finished before the buffer is read.
        using (var brotli = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true)) {
            brotli.Write(json);
        }

        return output.ToArray();
    }

    private static ReadOnlySpan<byte> Decompress(Stream compressed) {
        using var output = new MemoryStream();
        using var brotli = new BrotliStream(compressed, CompressionMode.Decompress, leaveOpen: true);

        brotli.CopyTo(output);

        return output.ToArray();
    }
}
