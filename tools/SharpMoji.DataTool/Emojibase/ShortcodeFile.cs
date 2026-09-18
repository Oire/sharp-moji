using System.Collections.Immutable;
using System.Text.Json;

namespace Oire.SharpMoji.DataTool.Emojibase;

/// <summary>
/// Reads an Emojibase shortcode file: a map of hexcode to one or more shortcodes.
/// </summary>
/// <remarks>
/// <para>
/// Shortcodes are a separate data axis from <c>data.json</c> — they live in
/// <c>{locale}/shortcodes/{preset}.json</c> and have their own load path (docs/SPEC.md 3.4). The
/// February 2026 draft modeled them as a field on the emoji record, which they are not.
/// </para>
/// <para>
/// Values are <strong>string or array of string</strong>, the fifth polymorphic field in this
/// data after <c>tone</c>, <c>emoticon</c>, <c>text</c> and <c>version</c>. Parsing with a plain
/// <c>Dictionary&lt;string, string&gt;</c> throws on roughly a quarter of the entries.
/// </para>
/// </remarks>
public static class ShortcodeFile {
    /// <summary>The presets Emojibase publishes for English.</summary>
    /// <remarks>
    /// Every other locale has only <c>cldr</c> and <c>cldr-native</c>, neither of which is shipped.
    /// </remarks>
    public static readonly ImmutableArray<string> EnglishPresets =
        ["cldr", "cldr-native", "emojibase", "emojibase-legacy", "github", "iamcal", "joypixels"];

    /// <summary>Reads one preset, normalizing both value shapes to a list.</summary>
    /// <returns>Hexcode to shortcodes, or empty when the preset is not present for that locale.</returns>
    public static Dictionary<string, string[]> Read(string locale, string preset) {
        var path = Path.Combine(
            EmojiCorpus.Root ?? throw new InvalidOperationException(EmojiCorpus.SkipReason),
            locale,
            "shortcodes",
            $"{preset}.json");

        if (!File.Exists(path)) {
            return [];
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in document.RootElement.EnumerateObject()) {
            result[property.Name] = property.Value.ValueKind switch {
                JsonValueKind.String => [property.Value.GetString()!],
                JsonValueKind.Array => [.. property.Value.EnumerateArray().Select(v => v.GetString()!)],
                _ => throw new InvalidDataException(
                    $"Shortcode for '{property.Name}' in {locale}/{preset} is {property.Value.ValueKind}, "
                    + "expected a string or an array."),
            };
        }

        return result;
    }
}
