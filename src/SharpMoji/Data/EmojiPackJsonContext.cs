using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oire.SharpMoji.Data;

/// <summary>
/// Source-generated serialization for the embedded pack format.
/// </summary>
/// <remarks>
/// Reflection-based <c>JsonSerializer</c> overloads break trimming and NativeAOT, both of which
/// SharpMoji promises to support (<c>docs/SPEC.md</c> section 2). Every read and write of a pack
/// goes through this context; the console sample publishes AOT on every CI run to keep that
/// honest.
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(StructurePack))]
[JsonSerializable(typeof(StringPack))]
[JsonSerializable(typeof(ShortcodePack))]
internal sealed partial class EmojiPackJsonContext: JsonSerializerContext;
