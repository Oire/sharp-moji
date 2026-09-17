using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oire.SharpMoji.Tests.Emojibase;

/// <summary>
/// Locates and loads the raw Emojibase corpus used by the Phase 0 data-assumption tests.
/// </summary>
/// <remarks>
/// The corpus is a test fixture, not product data: it is ~22 MB, it is not committed, and the
/// library never fetches it. Run <c>scripts/fetch-emoji-corpus.ps1</c> to populate it. When it is
/// absent the tests skip, so <c>dotnet test</c> still works offline.
/// </remarks>
internal static class EmojiCorpus {
    /// <summary>The 28 locales Emojibase publishes. Hebrew is not among them — see SPEC section 3.1.</summary>
    public static readonly ImmutableArray<string> Locales =
    [
        "bn", "da", "de", "en", "en-gb", "es", "es-mx", "et", "fi", "fr", "hi", "hu", "it", "ja",
        "ko", "lt", "ms", "nb", "nl", "pl", "pt", "ru", "sv", "th", "uk", "vi", "zh", "zh-hant"
    ];

    private static readonly Lazy<string?> RootPath = new(FindRoot);

    /// <summary>Gets the corpus directory, or <see langword="null"/> when it has not been fetched.</summary>
    public static string? Root => RootPath.Value;

    /// <summary>Gets a value indicating whether the corpus is available to test against.</summary>
    public static bool IsAvailable => Root is not null;

    /// <summary>The reason to show when skipping, or <see langword="null"/> when there is none.</summary>
    public static string? SkipReason => IsAvailable
        ? null
        : $"Emojibase corpus {SharpMojiData.EmojibaseVersion} not found. Run scripts/fetch-emoji-corpus.ps1.";

    /// <summary>Deserializes one locale's <c>data.json</c>.</summary>
    public static ImmutableArray<EmojibaseEmoji> Load(string locale) {
        var json = File.ReadAllText(PathTo(locale, "data.json"));
        var emoji = JsonSerializer.Deserialize<ImmutableArray<EmojibaseEmoji>>(json, Options);

        return emoji.IsDefault
            ? throw new InvalidOperationException($"Locale '{locale}' deserialized to null.")
            : emoji;
    }

    /// <summary>Reads one locale's raw JSON without deserializing it.</summary>
    public static string ReadRaw(string locale, string file = "data.json") => File.ReadAllText(PathTo(locale, file));

    public static string PathTo(string locale, string file) =>
        Path.Combine(Root ?? throw new InvalidOperationException("Corpus not available."), locale, file);

    /// <summary>
    /// Serializer settings for the corpus.
    /// </summary>
    /// <remarks>
    /// <see cref="JsonUnmappedMemberHandling.Disallow"/> is the point of Phase 0: an upstream field
    /// the model does not know about throws instead of being silently dropped. That is exactly the
    /// failure mode that produced the February 2026 draft's invented data model.
    /// </remarks>
    public static readonly JsonSerializerOptions Options = new() {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,

        // Deliberately NOT DefaultIgnoreCondition.WhenWritingNull. Upstream always emits `text`,
        // writing "" where there is no text presentation, and the model normalizes that to null.
        // A global ignore-when-null rule would drop the property instead of letting
        // EmptyStringAsNullConverter write "" back, and the round trip would lose a field that is
        // genuinely there. Properties that really are optional carry their own [JsonIgnore].
    };

    private static string? FindRoot() {
        // Walk up from the test assembly to the repository root, which is where .emoji-corpus lives.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null) {
            var candidate = Path.Combine(directory.FullName, ".emoji-corpus", SharpMojiData.EmojibaseVersion);

            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "en", "data.json"))) {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
