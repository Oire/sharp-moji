using System.Collections.Frozen;
using System.Collections.Immutable;
using Oire.SharpMoji.Data;

namespace Oire.SharpMoji;

/// <summary>
/// The shortcode lookup, shared by every catalog.
/// </summary>
/// <remarks>
/// <para>
/// Shortcodes do not vary by language in the shipped data, so this is built once for the process
/// rather than per catalog. That also means a Ukrainian catalog still resolves <c>:+1:</c>, which
/// is what an application wants: those conventions are not really English, they are just
/// conventions.
/// </para>
/// <para>
/// Built lazily, so an application that never touches a shortcode never pays for the index.
/// </para>
/// </remarks>
internal static class ShortcodeIndex {
    /// <summary>The presets in lookup order. Cldr first: it is the only complete one.</summary>
    public static readonly ImmutableArray<ShortcodePreset> Presets =
    [
        ShortcodePreset.Cldr,
        ShortcodePreset.Emojibase,
        ShortcodePreset.GitHub,
        ShortcodePreset.Iamcal,
        ShortcodePreset.JoyPixels,
        ShortcodePreset.EmojibaseLegacy,
        ShortcodePreset.CldrNative,
    ];

    private static readonly Lazy<Tables> Loaded = new(Build);

    /// <summary>Resolves a shortcode to a hexcode within one preset.</summary>
    public static string? Find(ShortcodePreset preset, string shortcode) =>
        Loaded.Value.ByShortcode.TryGetValue(preset, out var table)
            ? table.GetValueOrDefault(shortcode)
            : null;

    /// <summary>Returns the shortcodes one preset gives an emoji.</summary>
    public static ImmutableArray<string> For(ShortcodePreset preset, string hexcode) =>
        Loaded.Value.ByHexcode.TryGetValue(preset, out var table)
            ? table.GetValueOrDefault(hexcode, [])
            : [];

    private static Tables Build() {
        var pack = EmbeddedPackReader.ReadShortcodes();

        var byShortcode = new Dictionary<ShortcodePreset, FrozenDictionary<string, string>>();
        var byHexcode = new Dictionary<ShortcodePreset, FrozenDictionary<string, ImmutableArray<string>>>();

        foreach (var (name, entries) in pack.Presets) {
            if (ParsePreset(name) is not { } preset) {
                continue;
            }

            var forward = new Dictionary<string, ImmutableArray<string>>(entries.Count, StringComparer.OrdinalIgnoreCase);
            var reverse = new Dictionary<string, string>(entries.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var (hexcode, shortcodes) in entries) {
                forward[hexcode] = [.. shortcodes];

                foreach (var shortcode in shortcodes) {
                    // First writer wins. A preset can map two hexcodes to one shortcode, and there
                    // is no principled way to choose between them, so the earlier entry stands
                    // rather than the iteration order deciding silently on each run.
                    reverse.TryAdd(shortcode, hexcode);
                }
            }

            byHexcode[preset] = forward.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            byShortcode[preset] = reverse.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        return new Tables(byShortcode.ToFrozenDictionary(), byHexcode.ToFrozenDictionary());
    }

    private static ShortcodePreset? ParsePreset(string name) => name switch {
        "cldr" => ShortcodePreset.Cldr,
        "cldr-native" => ShortcodePreset.CldrNative,
        "emojibase" => ShortcodePreset.Emojibase,
        "emojibase-legacy" => ShortcodePreset.EmojibaseLegacy,
        "github" => ShortcodePreset.GitHub,
        "iamcal" => ShortcodePreset.Iamcal,
        "joypixels" => ShortcodePreset.JoyPixels,
        _ => null,
    };

    private sealed record Tables(
        FrozenDictionary<ShortcodePreset, FrozenDictionary<string, string>> ByShortcode,
        FrozenDictionary<ShortcodePreset, FrozenDictionary<string, ImmutableArray<string>>> ByHexcode);
}
