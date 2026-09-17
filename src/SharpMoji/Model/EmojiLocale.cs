using System.Collections.Immutable;
using Oire.SharpMoji.Data;

namespace Oire.SharpMoji;

/// <summary>
/// A language SharpMoji carries emoji text for.
/// </summary>
/// <remarks>
/// Every language listed in <see cref="All"/> is embedded in the package, so any of them can be
/// loaded with no I/O, no download and no failure path. See <c>docs/SPEC.md</c> section 7.
/// </remarks>
public sealed record EmojiLocale {
    /// <summary>The language code as used by CLDR and Emojibase, for example <c>"uk"</c>.</summary>
    public required string Code { get; init; }

    /// <summary>The language's name in English, for example <c>"Ukrainian"</c>.</summary>
    public required string EnglishName { get; init; }

    /// <summary>The language's name in itself, for example <c>"українська"</c>.</summary>
    /// <remarks>What a language picker should display, so speakers can recognize their own.</remarks>
    public required string NativeName { get; init; }

    /// <summary>Whether the language is written right to left.</summary>
    /// <remarks>
    /// SharpMoji never composes display text, so this changes nothing about what it returns. It is
    /// exposed so a UI can set text direction without a second lookup table of its own.
    /// </remarks>
    public bool IsRightToLeft { get; init; }

    private static readonly Lazy<ImmutableArray<EmojiLocale>> AllLocales = new(() =>
    [
        .. EmbeddedPackReader.ReadStructure().Locales
            .Select(l => new EmojiLocale
            {
                Code = l.Code,
                EnglishName = l.EnglishName,
                NativeName = l.NativeName,
                IsRightToLeft = l.IsRightToLeft,
            })
            .OrderBy(l => l.Code, StringComparer.Ordinal)
    ]);

    /// <summary>Every embedded language, ordered by code.</summary>
    public static ImmutableArray<EmojiLocale> All => AllLocales.Value;

    /// <summary>Finds a language by code, case-insensitively.</summary>
    /// <param name="code">A language code such as <c>"uk"</c> or <c>"zh-Hant"</c>.</param>
    /// <returns>The language, or <see langword="null"/> when it is not one of the embedded ones.</returns>
    public static EmojiLocale? Find(string? code) {
        if (string.IsNullOrWhiteSpace(code)) {
            return null;
        }

        var trimmed = code.Trim();

        // Codes are compared case-insensitively because callers pass through values from settings
        // files and UI state, where "zh-hant" and "zh-Hant" both turn up.
        return All.FirstOrDefault(l => string.Equals(l.Code, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Returns the code.</summary>
    public override string ToString() => Code;
}
